NuGetPatcher patcher = new(args[0]);
patcher.Patch();
patcher.Save(args.Length > 1 ? args[1] : null);
