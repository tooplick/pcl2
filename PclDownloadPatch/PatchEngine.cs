using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class McDownloadPatch
{
    static ModuleDefinition mod;
    static Action<string> log;
    static string dependencyDirectory;

    public static void SetDependencyDirectory(string directory)
    {
        dependencyDirectory = directory;
    }

    public static void ApplyPatch(string src, string dst, Action<string> logMessage)
    {
        if (string.IsNullOrWhiteSpace(src)) throw new ArgumentException("请选择输入 exe。", "src");
        if (string.IsNullOrWhiteSpace(dst)) throw new ArgumentException("请指定输出 exe。", "dst");
        log = logMessage ?? delegate { };

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(src)));
        if (!string.IsNullOrEmpty(dependencyDirectory)) resolver.AddSearchDirectory(dependencyDirectory);
        var asm = AssemblyDefinition.ReadAssembly(src, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
        mod = asm.MainModule;

        Version sourceVersion = asm.Name.Version;
        if (sourceVersion < new Version(2, 99, 0, 0))
            throw new InvalidOperationException("该版本不需要应用下载限制补丁。");

        Log("[版本] PCL " + sourceVersion);
        ApplyVersionStrategy(sourceVersion);

        string resDir = ExtractEmbeddedAssemblies();
        if (resDir != null) resolver.AddSearchDirectory(resDir);

        asm.Write(dst);
        Log("[完成] 已写入补丁副本：" + dst);
    }

    static void ApplyVersionStrategy(Version version)
    {
        // Restrictions start at 2.99.0.0. Apply the restricted-build strategy to that
        // version and later releases; structural mismatches fail before source replacement.
        if (version >= new Version(2, 99, 0, 0))
        {
            PatchRestrictedBuild299();
            return;
        }

        throw new InvalidOperationException("尚未适配 PCL " + version + " 的补丁策略。");
    }

    static void PatchRestrictedBuild299()
    {
        PatchLoaderInit();
        Log("[完成] PageDownloadInstall::LoaderInit");

        PatchModifyVisibility();
        Log("[完成] PageDownloadModify::LoadInstanceFinished");

        PatchModpackDispatch();
        Log("[完成] ModModpack::ModpackInstall(String,String,String)");

        PatchModpackInstallDialog();
        Log("[完成] ModModpack::ModpackInstall()");

        PatchResourceDetailInstall();
        Log("[完成] PageDownloadResourceDetail::InitializeStackResourceVersions");
    }

    static void Log(string message)
    {
        if (log != null) log(message);
    }

    // ---------------- helpers ----------------

    // PCL embeds its dependency assemblies as .resources blobs; dump them so Cecil
    // can resolve type references while writing the patched module.
    static string ExtractEmbeddedAssemblies()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pclpatch_res");
        try
        {
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            System.IO.Directory.CreateDirectory(dir);
        }
        catch { return null; }

        int n = 0;
        foreach (var res in mod.Resources)
        {
            var er = res as EmbeddedResource;
            if (er == null || !er.Name.EndsWith(".resources", StringComparison.Ordinal)) continue;
            try
            {
                using (var s = er.GetResourceStream())
                using (var rr = new System.Resources.ResourceReader(s))
                {
                    var en = rr.GetEnumerator();
                    while (en.MoveNext())
                    {
                        var bytes = en.Value as byte[];
                        if (bytes == null || bytes.Length < 2 || bytes[0] != 0x4D || bytes[1] != 0x5A) continue;
                        var name = (string)en.Key;
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".dll"), bytes);
                        n++;
                    }
                }
            }
            catch { }
        }
        Log("[信息] 已提取内嵌依赖程序集：" + n);
        return n > 0 ? dir : null;
    }


    static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> ts)
    {
        foreach (var t in ts)
        {
            yield return t;
            if (t.HasNestedTypes)
                foreach (var n in AllTypes(t.NestedTypes)) yield return n;
        }
    }

    static IEnumerable<MethodDefinition> AllMethods()
    {
        return AllTypes(mod.Types).SelectMany(t => t.Methods);
    }

    static TypeDefinition FindType(string fullName)
    {
        var t = AllTypes(mod.Types).FirstOrDefault(x => x.FullName == fullName);
        if (t == null) throw new Exception("type not found: " + fullName);
        return t;
    }

    static MethodDefinition FindMethod(TypeDefinition t, string name, int paramCount)
    {
        var m = t.Methods.FirstOrDefault(x => x.Name == name && x.Parameters.Count == paramCount);
        if (m == null) throw new Exception("method not found: " + t.FullName + "::" + name + "/" + paramCount);
        return m;
    }

    static MethodReference FindMethodRef(string declTypeContains, string name, int paramCount)
    {
        var r = FindMethodRefs(declTypeContains, name, paramCount).FirstOrDefault();
        if (r != null) return r;
        throw new Exception("methodref not found: " + declTypeContains + "::" + name + "/" + paramCount);
    }

    static IEnumerable<MethodReference> FindMethodRefs(string declTypeContains, string name, int paramCount)
    {
        foreach (var m in AllMethods())
        {
            if (!m.HasBody) continue;
            foreach (var i in m.Body.Instructions)
            {
                var mr = i.Operand as MethodReference;
                if (mr == null || mr.Name != name || mr.Parameters.Count != paramCount) continue;
                if (declTypeContains != null && mr.DeclaringType.FullName.IndexOf(declTypeContains, StringComparison.Ordinal) < 0) continue;
                yield return mr;
            }
        }
    }

    static MethodReference MakeMethod(string name, TypeReference ret, TypeReference decl, bool hasThis, params TypeReference[] ps)
    {
        var mr = new MethodReference(name, ret, decl);
        mr.HasThis = hasThis;
        foreach (var p in ps) mr.Parameters.Add(new ParameterDefinition(p));
        return mr;
    }

    // Find an existing `ldstr <filter>; call OneOf::op_Implicit; newobj Nullable::.ctor; call SelectFile`
    // sequence so the injected call reuses references that are known to resolve.
    // The OneOf op_Implicit signature uses the generic parameter (!1) as its argument,
    // so the reference must be reused verbatim — a hand-built System.String signature
    // does not match at JIT time.
    static void ResolveSelectFileFilterRefs(MethodReference selectFile, ref MethodReference filterImplicit, ref MethodReference nullableCtor)
    {
        var targetType = selectFile.Parameters[3].ParameterType.FullName;
        MethodReference fallbackImpl = null, fallbackCtor = null;

        foreach (var m in AllMethods())
        {
            if (!m.HasBody) continue;
            var ins = m.Body.Instructions;
            for (int i = 3; i < ins.Count; i++)
            {
                var call = ins[i].Operand as MethodReference;
                if (call == null || call.Name != "SelectFile" || call.Parameters.Count != 4) continue;

                var nb = ins[i - 1].Operand as MethodReference;
                var impl = ins[i - 2].Operand as MethodReference;
                if (nb == null || nb.Name != ".ctor") continue;
                if (impl == null || impl.Name != "op_Implicit" || impl.Parameters.Count != 1) continue;
                if (nb.DeclaringType.FullName != targetType) continue;

                var pt = impl.Parameters[0].ParameterType;
                var gp = pt as GenericParameter;
                if (gp == null || gp.Position == 1)
                {
                    filterImplicit = impl;
                    nullableCtor = nb;
                    return;
                }
                if (fallbackImpl == null) { fallbackImpl = impl; fallbackCtor = nb; }
            }
        }

        if (fallbackImpl != null)
        {
            filterImplicit = fallbackImpl;
            nullableCtor = fallbackCtor;
            return;
        }

        throw new Exception("no usable SelectFile filter reference found");
    }

    static void RemoveRange(ILProcessor il, IList<Instruction> ins, int start, int end)
    {
        var dead = new List<Instruction>();
        for (int i = start; i <= end; i++) dead.Add(ins[i]);
        foreach (var d in dead) il.Remove(d);
    }

    // ---------------- Patch 1 ----------------
    // PageDownloadInstall.LoaderInit: replace the always-failing "close download" loader
    // with the real DlClientListLoader.

    static void PatchLoaderInit()
    {
        var t = FindType("PCL.PageDownloadInstall");
        var m = FindMethod(t, "LoaderInit", 0);
        var body = m.Body;
        var ins = body.Instructions;
        var il = body.GetILProcessor();

        int start = -1, end = -1;
        for (int i = 0; i < ins.Count; i++)
        {
            var mr = ins[i].Operand as MethodReference;
            if (start < 0 && mr != null && mr.Name == "get_PanAllBack") start = i + 1;
            if (start >= 0 && ins[i].OpCode.Code == Code.Newobj)
            {
                var c = (MethodReference)ins[i].Operand;
                if (c.DeclaringType.Name.StartsWith("LoaderTask`2")) { end = i; break; }
            }
        }
        if (start < 0 || end < 0) throw new Exception("LoaderInit pattern not found");
        if (ins[start].OpCode.Code != Code.Ldnull) throw new Exception("LoaderInit: expected ldnull at start, got " + ins[start]);

        var src = FindType("PCL.ModDownload");
        var loaderField = src.Fields.FirstOrDefault(f => f.Name == "DlClientListLoader");
        if (loaderField == null) throw new Exception("DlClientListLoader field not found");

        var tail = ins[end + 1];
        RemoveRange(il, ins, start, end);
        var a = Instruction.Create(OpCodes.Ldnull);
        var b = Instruction.Create(OpCodes.Ldsfld, loaderField);
        il.InsertBefore(tail, a);
        il.InsertBefore(tail, b);
    }

    // ---------------- Patch 2 ----------------
    // PageDownloadModify.LoadInstanceFinished: CardMinecraft.Visibility = Collapsed -> Visible

    static void PatchModifyVisibility()
    {
        var t = FindType("PCL.PageDownloadModify");
        var m = FindMethod(t, "LoadInstanceFinished", 1);
        var ins = m.Body.Instructions;

        for (int i = 0; i < ins.Count - 1; i++)
        {
            var mr = ins[i].Operand as MethodReference;
            if (mr != null && mr.Name == "get_CardMinecraft" && ins[i + 1].OpCode.Code == Code.Ldc_I4_2)
            {
                ins[i + 1].OpCode = OpCodes.Ldc_I4_0;
                ins[i + 1].Operand = null;
                return;
            }
        }
        throw new Exception("LoadInstanceFinished: CardMinecraft visibility pattern not found");
    }

    // ---------------- Patch 3 ----------------
    // ModModpack.ModpackInstall(File, InstanceName, Logo): the dead "Select Case PackType"
    // dispatch was removed at compile time. Append a replacement and retarget all
    // leave instructions that jumped to the guard throw.

    static void PatchModpackDispatch()
    {
        var t = FindType("PCL.ModModpack");
        var m = FindMethod(t, "ModpackInstall", 3);
        var body = m.Body;
        var ins = body.Instructions;
        var il = body.GetILProcessor();

        // locate `ldstr <guard msg>; newobj Exception::.ctor(string); throw`
        int throwSite = -1;
        for (int i = 0; i + 2 < ins.Count; i++)
        {
            if (ins[i].OpCode.Code != Code.Ldstr) continue;
            if (ins[i + 1].OpCode.Code != Code.Newobj) continue;
            var c = (MethodReference)ins[i + 1].Operand;
            if (c.DeclaringType.FullName != "System.Exception" || c.Parameters.Count != 1) continue;
            if (ins[i + 2].OpCode.Code != Code.Throw) continue;
            throwSite = i;
            break;
        }
        if (throwSite < 0) throw new Exception("ModpackInstall(3): guard throw not found");
        var throwInstr = ins[throwSite];

        int leaveCount = ins.Count(i => (i.OpCode.Code == Code.Leave || i.OpCode.Code == Code.Leave_S) && i.Operand == throwInstr);
        if (leaveCount < 5) throw new Exception("ModpackInstall(3): unexpected leave count " + leaveCount);

        var vResult = body.Variables[0];
        var vArchive = body.Variables[1];
        var vBase = body.Variables[2];
        var vType = body.Variables[4];

        var fCurseForge = FindMethod(t, "InstallPackCurseForge", 5);
        var fModrinth = FindMethod(t, "InstallPackModrinth", 5);
        var fHMCL = FindMethod(t, "InstallPackHMCL", 3);
        var fMMC = FindMethod(t, "InstallPackMMC", 3);
        var fMCBBS = FindMethod(t, "InstallPackMCBBS", 4);
        var fLauncherPack = FindMethod(t, "InstallPackLauncherPack", 3);
        var fCompress = FindMethod(t, "InstallPackCompress", 2);

        // targets
        var case0 = Instruction.Create(OpCodes.Ldarg_0);
        var case1 = Instruction.Create(OpCodes.Ldarg_0);
        var case2 = Instruction.Create(OpCodes.Ldarg_0);
        var case3 = Instruction.Create(OpCodes.Ldarg_0);
        var case4 = Instruction.Create(OpCodes.Ldarg_0);
        var case9 = Instruction.Create(OpCodes.Ldarg_0);
        var caseDefault = Instruction.Create(OpCodes.Ldarg_0);
        var endBlock = Instruction.Create(OpCodes.Ldloc, vResult);
        var retInstr = Instruction.Create(OpCodes.Ret);

        var dispatchStart = Instruction.Create(OpCodes.Ldloc, vType);

        var prog = new List<Instruction>();
        prog.Add(dispatchStart);
        prog.Add(Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)9));
        prog.Add(Instruction.Create(OpCodes.Beq, case9));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vType));
        prog.Add(Instruction.Create(OpCodes.Switch, new[] { case0, case1, case2, case3, case4 }));
        prog.Add(Instruction.Create(OpCodes.Br, caseDefault));

        // case 0: CurseForge(File, Archive, Base, InstanceName, Logo)
        prog.Add(case0);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Ldarg_1));
        prog.Add(Instruction.Create(OpCodes.Ldarg_2));
        prog.Add(Instruction.Create(OpCodes.Call, fCurseForge));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // case 1: HMCL(File, Archive, Base)
        prog.Add(case1);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Call, fHMCL));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // case 2: MMC(File, Archive, Base)
        prog.Add(case2);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Call, fMMC));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // case 3: MCBBS(File, Archive, Base, InstanceName)
        prog.Add(case3);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Ldarg_1));
        prog.Add(Instruction.Create(OpCodes.Call, fMCBBS));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // case 4: Modrinth(File, Archive, Base, InstanceName, Logo)
        prog.Add(case4);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Ldarg_1));
        prog.Add(Instruction.Create(OpCodes.Ldarg_2));
        prog.Add(Instruction.Create(OpCodes.Call, fModrinth));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // case 9: LauncherPack(File, Archive, Base)
        prog.Add(case9);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Ldloc, vBase));
        prog.Add(Instruction.Create(OpCodes.Call, fLauncherPack));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        // default: Compress(File, Archive)
        prog.Add(caseDefault);
        prog.Add(Instruction.Create(OpCodes.Ldloc, vArchive));
        prog.Add(Instruction.Create(OpCodes.Call, fCompress));
        prog.Add(Instruction.Create(OpCodes.Stloc, vResult));
        prog.Add(Instruction.Create(OpCodes.Br, endBlock));

        prog.Add(endBlock);
        prog.Add(retInstr);

        // The outer Using's Finally handler ends at end-of-method; pin it before appending.
        foreach (var h in body.ExceptionHandlers)
        {
            if (h.HandlerType == ExceptionHandlerType.Finally && h.HandlerEnd == null)
                h.HandlerEnd = dispatchStart;
        }

        foreach (var p in prog) il.Append(p);

        // retarget guard jumps
        foreach (var i in ins)
        {
            if ((i.OpCode.Code == Code.Leave || i.OpCode.Code == Code.Leave_S) && i.Operand == throwInstr)
            {
                i.OpCode = OpCodes.Leave;
                i.Operand = dispatchStart;
            }
        }
    }

    // ---------------- Patch 4 ----------------
    // ModModpack.ModpackInstall(): rebuild the dialog + background install entry point.

    static void PatchModpackInstallDialog()
    {
        var t = FindType("PCL.ModModpack");
        var m = FindMethod(t, "ModpackInstall", 0);
        var mWorkerTarget = FindMethod(t, "ModpackInstall", 3);
        var body = m.Body;

        // grab member refs we need before wiping the body
        var selectFile = FindMethodRef("MeloongCore.Wpf.Dialogs", "SelectFile", 4);
        var hint = FindMethodRef("PCL.ModMain", "Hint", 3);
        var runInThread = FindMethodRef("PCL.ModBase", "RunInThread", 1);
        var actionCtor = FindMethodRef("System.Action", ".ctor", 2);

        var listType = selectFile.ReturnType;
        var isNullOrEmpty = MakeMethod("IsNullOrEmpty", mod.TypeSystem.Boolean, mod.TypeSystem.String, false, mod.TypeSystem.String);
        var exType = mod.ImportReference(typeof(System.Exception));
        var getMessage = MakeMethod("get_Message", mod.TypeSystem.String, exType, true);

        // filter: OneOf<..., string> implicit conversion + Nullable<T>.ctor,
        // borrowed from an existing SelectFile call site that passes a filter.
        MethodReference filterImplicit = null, nullableCtor = null;
        ResolveSelectFileFilterRefs(selectFile, ref filterImplicit, ref nullableCtor);

        // reuse an already-instantiated Enumerable.FirstOrDefault<string>(IEnumerable<string>)
        MethodReference firstOrDefault = null;
        foreach (var cand in FindMethodRefs("System.Linq.Enumerable", "FirstOrDefault", 1))
        {
            var gi = cand as GenericInstanceMethod;
            if (gi != null && gi.GenericArguments.Count == 1 && gi.GenericArguments[0].FullName == "System.String")
            {
                firstOrDefault = cand;
                break;
            }
        }
        if (firstOrDefault == null)
        {
            var def = FindMethodRefs("System.Linq.Enumerable", "FirstOrDefault", 1).FirstOrDefault();
            if (def == null) throw new Exception("Enumerable.FirstOrDefault<T> not found");
            var gi = new GenericInstanceMethod(def);
            gi.GenericArguments.Add(mod.TypeSystem.String);
            firstOrDefault = gi;
        }

        var workerField = new FieldDefinition("PclPatchPendingPackFile", FieldAttributes.Private | FieldAttributes.Static, mod.TypeSystem.String);
        t.Fields.Add(workerField);

        var worker = new MethodDefinition("PclPatchModpackInstallWorker",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, mod.TypeSystem.Void);
        t.Methods.Add(worker);
        {
            var wb = worker.Body;
            wb.InitLocals = true;
            var exVar = new VariableDefinition(exType);
            wb.Variables.Add(exVar);
            var wil = wb.GetILProcessor();

            var afterTry = Instruction.Create(OpCodes.Ret);
            var handlerStart = Instruction.Create(OpCodes.Stloc, exVar);

            var tryStart = Instruction.Create(OpCodes.Ldsfld, workerField);
            wil.Append(tryStart);
            wil.Append(Instruction.Create(OpCodes.Ldnull));
            wil.Append(Instruction.Create(OpCodes.Ldnull));
            wil.Append(Instruction.Create(OpCodes.Call, mWorkerTarget));
            wil.Append(Instruction.Create(OpCodes.Pop));
            wil.Append(Instruction.Create(OpCodes.Leave, afterTry));

            wil.Append(handlerStart);
            wil.Append(Instruction.Create(OpCodes.Ldloc, exVar));
            wil.Append(Instruction.Create(OpCodes.Callvirt, getMessage));
            wil.Append(Instruction.Create(OpCodes.Ldc_I4_2)); // HintType.Red
            wil.Append(Instruction.Create(OpCodes.Ldc_I4_1)); // Log := True
            wil.Append(Instruction.Create(OpCodes.Call, hint));
            wil.Append(Instruction.Create(OpCodes.Leave, afterTry));

            wil.Append(afterTry);

            var eh = new ExceptionHandler(ExceptionHandlerType.Catch);
            eh.CatchType = exType;
            eh.TryStart = tryStart;
            eh.TryEnd = handlerStart;
            eh.HandlerStart = handlerStart;
            eh.HandlerEnd = afterTry;
            wb.ExceptionHandlers.Add(eh);
        }

        // rebuild ModpackInstall()
        body.Instructions.Clear();
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        body.InitLocals = true;

        var vList = new VariableDefinition(listType);
        var vFile = new VariableDefinition(mod.TypeSystem.String);
        body.Variables.Add(vList);
        body.Variables.Add(vFile);

        var il = body.GetILProcessor();

        var empty = Instruction.Create(OpCodes.Ret);
        var afterLoad = Instruction.Create(OpCodes.Ret);

        il.Append(Instruction.Create(OpCodes.Ldstr, "\u9009\u62e9\u6574\u5408\u5305\u538b\u7f29\u6587\u4ef6"));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(Instruction.Create(OpCodes.Ldnull));
        il.Append(Instruction.Create(OpCodes.Ldstr,
            "\u6574\u5408\u5305\u6587\u4ef6(*.rar;*.zip;*.mrpack)|*.rar;*.zip;*.mrpack"));
        il.Append(Instruction.Create(OpCodes.Call, filterImplicit));
        il.Append(Instruction.Create(OpCodes.Newobj, nullableCtor));
        il.Append(Instruction.Create(OpCodes.Call, selectFile));
        il.Append(Instruction.Create(OpCodes.Stloc, vList));

        il.Append(Instruction.Create(OpCodes.Ldloc, vList));
        il.Append(Instruction.Create(OpCodes.Call, firstOrDefault));
        il.Append(Instruction.Create(OpCodes.Stloc, vFile));

        il.Append(Instruction.Create(OpCodes.Ldloc, vFile));
        il.Append(Instruction.Create(OpCodes.Call, isNullOrEmpty));
        il.Append(Instruction.Create(OpCodes.Brtrue, empty));

        il.Append(Instruction.Create(OpCodes.Ldloc, vFile));
        il.Append(Instruction.Create(OpCodes.Stsfld, workerField));

        il.Append(Instruction.Create(OpCodes.Ldnull));
        il.Append(Instruction.Create(OpCodes.Ldftn, worker));
        il.Append(Instruction.Create(OpCodes.Newobj, actionCtor));
        il.Append(Instruction.Create(OpCodes.Call, runInThread));
        il.Append(Instruction.Create(OpCodes.Br, afterLoad));

        il.Append(empty);
        il.Append(afterLoad);
    }

    // ---------------- Patch 5 ----------------
    // PageDownloadResourceDetail.InitializeStackResourceVersions: restore the
    // "install" click handler for modpack resource versions.

    static void PatchResourceDetailInstall()
    {
        var t = FindType("PCL.PageDownloadResourceDetail");
        var m = FindMethod(t, "InitializeStackResourceVersions", 1);
        var body = m.Body;
        var ins = body.Instructions;
        var il = body.GetILProcessor();

        int callIdx = -1;
        MethodReference toListItem = null;
        for (int i = 0; i < ins.Count; i++)
        {
            var mr = ins[i].Operand as MethodReference;
            if (mr != null && mr.Name == "ToListItem" && mr.Parameters.Count == 3)
            {
                callIdx = i;
                toListItem = mr;
                break;
            }
        }
        if (callIdx < 0) throw new Exception("ToListItem call not found");

        int start = callIdx - 5;
        if (start < 0) throw new Exception("ToListItem args not found");
        if (ins[start].OpCode.Code != Code.Ldarg_0) throw new Exception("unexpected arg pattern: " + ins[start]);

        var ctorRef = ((MethodReference)ins[start + 2].Operand);
        var saveLdftn = ins[start + 1];

        var targetField = mod.ImportReference(ins.First(i => i.OpCode.Code == Code.Ldfld &&
            ((FieldReference)i.Operand).Name == "TargetResourceType").Operand as FieldReference);

        var installClick = FindMethod(t, "Install_Click", 2);
        var vBool = body.Variables[1];
        var tail = ins[callIdx + 1];

        var elseLabel = Instruction.Create(OpCodes.Ldarg_0);
        var doneLabel = Instruction.Create(OpCodes.Ldloc, vBool);

        RemoveRange(il, ins, start, callIdx);

        var seq = new List<Instruction>();
        seq.Add(Instruction.Create(OpCodes.Ldarg_0));
        seq.Add(Instruction.Create(OpCodes.Ldfld, targetField));
        seq.Add(Instruction.Create(OpCodes.Ldc_I4_2)); // ResourceTypes.ModPack
        seq.Add(Instruction.Create(OpCodes.Bne_Un, elseLabel));

        // install variant: ToListItem(Install_Click, Save_Click, HasDuplicates)
        seq.Add(Instruction.Create(OpCodes.Ldarg_0));
        seq.Add(Instruction.Create(OpCodes.Ldftn, installClick));
        seq.Add(Instruction.Create(OpCodes.Newobj, ctorRef));
        seq.Add(Instruction.Create(OpCodes.Ldarg_0));
        seq.Add(Instruction.Create(OpCodes.Ldftn, (MethodReference)saveLdftn.Operand));
        seq.Add(Instruction.Create(OpCodes.Newobj, ctorRef));
        seq.Add(Instruction.Create(OpCodes.Br, doneLabel));

        // save-only variant: ToListItem(Save_Click, Nothing, HasDuplicates)
        seq.Add(elseLabel);
        seq.Add(Instruction.Create(OpCodes.Ldftn, (MethodReference)saveLdftn.Operand));
        seq.Add(Instruction.Create(OpCodes.Newobj, ctorRef));
        seq.Add(Instruction.Create(OpCodes.Ldnull));

        seq.Add(doneLabel);
        seq.Add(Instruction.Create(OpCodes.Callvirt, toListItem));

        foreach (var s in seq) il.InsertBefore(tail, s);
    }
}
