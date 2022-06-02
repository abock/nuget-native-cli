//
// Author:
//   Aaron Bockover <abock@microsoft.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

sealed class NuGetPatcher
{
    readonly ModuleDefinition _nugetModule;
    readonly ModuleDefinition _stubsModule;
    readonly string _nugetVersion;

    public NuGetPatcher(string nugetAssemblyPath)
        : this(
            nugetAssemblyPath,
            Path.Join(AppContext.BaseDirectory, "shims.dll"))
    {
    }

    public NuGetPatcher(
        string nugetAssemblyPath,
        string netfxStubsAssemblyPath)
    {
        _nugetVersion = FileVersionInfo.GetVersionInfo(nugetAssemblyPath)
            ?.FileVersion ?? "<unknown-version>";

        _nugetModule = ModuleDefinition.ReadModule(
            nugetAssemblyPath,
            new()
            {
                ReadWrite = true
            });

        _stubsModule = ModuleDefinition.ReadModule(netfxStubsAssemblyPath);
    }

    public void Save(string? outputAssemblyPath = null)
    {
        if (outputAssemblyPath is not null)
            _nugetModule.Write(outputAssemblyPath);
        else
            _nugetModule.Write();
    }

    public void Patch()
    {
        foreach (var type in _nugetModule.GetTypes())
        {
            PatchTypeDefinitionShim(type);
        }
    }

    void PatchTypeDefinitionShim(TypeDefinition typeDefinition)
    {
        if (TryGetStubTypeReference(typeDefinition.BaseType, out var stubBaseType))
        {
            typeDefinition.BaseType = stubBaseType;
        }

        foreach (var iface in typeDefinition.Interfaces)
        {
            if (TryGetStubTypeReference(iface.InterfaceType, out var stubInterfaceType))
            {
                iface.InterfaceType = stubInterfaceType;
            }
        }

        if (typeDefinition.HasMethods)
        {
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                PatchMethodDefinitionShim(methodDefinition);
            }
        }
    }

    void PatchMethodDefinitionShim(MethodDefinition methodDefinition)
    {
        if (!methodDefinition.HasBody)
        {
            return;
        }

        // Because the assembly does not exist on disk, OutputNuGetVersion will fail.
        //
        // https://github.com/NuGet/NuGet.Client/blob/release-5.7.x/src/NuGet.Clients/NuGet.CommandLine/Commands/Command.cs#L163
        // https://github.com/NuGet/NuGet.Client/blob/release-6.2.x/src/NuGet.Clients/NuGet.CommandLine/Commands/Command.cs#L163
        //
        // Replacing the following:
        //
        //   var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        //   var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation).FileVersion;
        //
        // With:
        //
        //   var version = "...";
        if (methodDefinition.DeclaringType.FullName == "NuGet.CommandLine.Command" &&
            methodDefinition.Name == "OutputNuGetVersion")
        {
            var instruction = methodDefinition.Body.Instructions.First(
                i => i.OpCode == OpCodes.Callvirt &&
                    i.Operand is MethodReference mr &&
                    mr.FullName == "System.String System.Reflection.Assembly::get_Location()");

            instruction.Previous.OpCode = OpCodes.Nop;
            instruction.Previous.Operand = null;
            instruction.OpCode = OpCodes.Nop;
            instruction.Operand = null;
            instruction.Next.OpCode = OpCodes.Nop;
            instruction.Next.Operand = null;
            instruction.Next.Next.OpCode = OpCodes.Ldstr;
            instruction.Next.Next.Operand = _nugetVersion + " (https://github.com/abock/nuget-native-cli)";

            return;
        }

        // Any other calls we redirect to the stubs assembly if applicable
        foreach (var instruction in methodDefinition.Body.Instructions)
        {
            if (instruction.Operand is MethodReference methodReference && (
                instruction.OpCode == OpCodes.Call ||
                instruction.OpCode == OpCodes.Callvirt ||
                instruction.OpCode == OpCodes.Calli) &&
                TryGetStubMethodReference(methodReference, out var stubMethodReference))
            {
                instruction.Operand = stubMethodReference;
            }
        }
    }

    static bool AreTypesSame(TypeReference a, TypeReference b)
        => a == b || a.FullName == b.FullName;

    static bool AreParametersSame(ParameterReference a, ParameterReference b)
        => AreTypesSame(a.ParameterType, b.ParameterType);

    bool TryGetStubMethodReference(
        MethodReference? method,
        [MaybeNullWhen(false)] out MethodReference stubMethodReference)
    {
        stubMethodReference = null;

        if (method?.DeclaringType is null ||
            !TryGetStubTypeReference(method.DeclaringType, out var stubTypeReference))
        {
            return false;
        }

        var stubTypeDefinition = stubTypeReference.Resolve();

        foreach (var stubMethodDefinition in stubTypeDefinition.Methods)
        {
            if (stubMethodDefinition.Name == method.Name &&
                AreTypesSame(stubMethodDefinition.ReturnType, method.ReturnType) &&
                stubMethodDefinition.HasThis == method.HasThis &&
                stubMethodDefinition.Parameters.Zip(method.Parameters).All(
                    z => AreParametersSame(z.First, z.Second)))
            {
                stubMethodReference = _nugetModule.ImportReference(stubMethodDefinition);
            }
        }

        return stubMethodReference is not null;
    }

    bool TryGetStubTypeReference(
        TypeReference? type,
        [MaybeNullWhen(false)] out TypeReference stubTypeReference)
    {
        stubTypeReference = null;
        if (type is not null && _stubsModule.GetType(
            "NuGet.NetFxStubs." + type.FullName) is TypeDefinition stubType)
            stubTypeReference = _nugetModule.ImportReference(stubType);
        return stubTypeReference is not null;
    }
}
