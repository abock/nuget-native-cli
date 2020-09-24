//
// Author:
//   Aaron Bockover <abock@microsoft.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

static class NuGetPatcher
{
    static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(
            args[0],
            new ReaderParameters
            {
                ReadWrite = true
            });

        Collection<Instruction> GetMethodIL(
            string typeName,
            string methodName)
            => module
                .GetTypes()
                .Single(typeDefinition => typeDefinition.FullName == typeName)
                .Methods
                .Single(method => method.Name == methodName)
                .Body
                .Instructions;

        var findAllMethodInstructions = GetMethodIL(
            "NuGet.CommandLine.ExtensionLocator",
            "FindAll");

        for (var i = 0; i < findAllMethodInstructions.Count; i++)
        {
            var instruction = findAllMethodInstructions[i];

            // We are looking for the IL that represents the boundary between the following two statements,
            // so we can insert an early return to avoid the bug when we are mkbundle'd:
            //
            // https://github.com/NuGet/NuGet.Client/blob/release-5.7.x/src/NuGet.Clients/NuGet.CommandLine/ExtensionLocator.cs#L101
            //
            // foreach (var directory in directories.Where(Directory.Exists)) { ... }
            //                                                                      ^- C: endfinally
            //
            // var nugetDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            //                                                ^.    ^- B: TypeDefintion operand (NuGet.CommandLine.Program)
            //                                                  `- A: ldtoken

            if (instruction.OpCode == OpCodes.Ldtoken && // A
                instruction.Previous?.OpCode == OpCodes.Endfinally && // B
                instruction.Operand is TypeDefinition typeDefinition && // C
                typeDefinition.FullName == "NuGet.CommandLine.Program")
            {
                instruction.OpCode = OpCodes.Ldloc_0;
                instruction.Operand = null;
                instruction.Next.OpCode = OpCodes.Ret;
                instruction.Next.Operand = null;

                var retIndex = i + 2;

                for (i = findAllMethodInstructions.Count - 1; i >= retIndex; i--)
                    findAllMethodInstructions.RemoveAt(i);

                break;
            }
        }

        var outputNugetVersionInstructions = GetMethodIL(
            "NuGet.CommandLine.Command",
            "OutputNuGetVersion");

        // Because the assembly does not exist on disk, OutputNuGetVersion will fail.
        //
        // https://github.com/NuGet/NuGet.Client/blob/release-5.7.x/src/NuGet.Clients/NuGet.CommandLine/Commands/Command.cs#L163
        //
        // Replacing the following:
        //
        //   var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        //   var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation).FileVersion;
        //
        // With:
        //
        //   var version = "...";

        ReplaceIL(
            outputNugetVersionInstructions,
            new[]
            {
                "call System.Reflection.Assembly System.Reflection.Assembly::GetExecutingAssembly()",
                "callvirt System.String System.Reflection.Assembly::get_Location()",
                "call System.Diagnostics.FileVersionInfo System.Diagnostics.FileVersionInfo::GetVersionInfo(System.String)",
                "callvirt System.String System.Diagnostics.FileVersionInfo::get_FileVersion()"
            },
            new[]
            {
                Instruction.Create(OpCodes.Ldstr, FileVersionInfo.GetVersionInfo(args[0]).FileVersion),
                Instruction.Create(OpCodes.Stloc_1),
            }
        );

        if (args.Length > 1)
            module.Write(args[1]);
        else
            module.Write();
    }

    static void ReplaceIL(
        Collection<Instruction> instructions,
        string[] match,
        Instruction[] replace)
    {
        var (start, end) = MatchIL(instructions, match);
        if (start >= 0 && end >= 0)
        {
            for (var i = end; i >= start; i--)
                instructions.RemoveAt(i);

            for (var i = replace.Length - 1; i >= 0; i--)
                instructions.Insert(start, replace[i]);
        }
    }

    static (int Start, int End) MatchIL(
        Collection<Instruction> instructions,
        params string[] match)
    {
        for (var i = 0; i + match.Length < instructions.Count; i++)
        {
            var start = i;
            var isMatch = true;

            for (var j = 0; j < match.Length; j++, i++)
            {
                var instructionString = instructions[i].OpCode.Name;
                if (instructions[i].Operand is object)
                    instructionString += $" {instructions[i].Operand}";

                if (instructionString != match[j])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
                return (start, start + match.Length);
        }

        return (-1, -1);
    }
}
