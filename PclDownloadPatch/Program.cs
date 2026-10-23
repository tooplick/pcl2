using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PclDownloadPatch
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            if (args.Length > 0) return RunCommandLine(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(AppDomain.CurrentDomain.BaseDirectory));
            return 0;
        }

        // 命令行静默模式：PCL Download Patch.exe <输入exe> [输出exe]。
        // 仅输入时与拖入窗口一致（原地替换）；带输出路径时只写补丁副本、不碰原文件。
        // 退出码：0 补丁成功；1 无需处理（无下载限制 / 已打过补丁）；2 处理失败。
        private static int RunCommandLine(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            string input = args[0];
            string output = args.Length > 1 ? args[1] : null;
            string temp = null;
            try
            {
                if (!File.Exists(input)) throw new FileNotFoundException("输入文件不存在。", input);
                input = Path.GetFullPath(input);

                Version version = MainForm.ReadPclVersion(input);
                Console.WriteLine("PCL " + version + " · " + Path.GetFileName(input));

                if (version < new Version(2, 99, 0, 0))
                {
                    Console.WriteLine("该版本无下载限制，无需处理。");
                    return 1;
                }
                if (McDownloadPatch.IsAlreadyPatched(input))
                {
                    Console.WriteLine("该文件已应用补丁，无需重复处理。");
                    return 1;
                }

                McDownloadPatch.SetDependencyDirectory(AppDomain.CurrentDomain.BaseDirectory);
                if (output != null)
                {
                    // 引擎的 Log 回调已打印「已写入补丁副本」，这里不再重复输出
                    McDownloadPatch.ApplyPatch(input, Path.GetFullPath(output), Console.WriteLine);
                }
                else
                {
                    temp = input + "." + Guid.NewGuid().ToString("N") + ".patching.tmp";
                    McDownloadPatch.ApplyPatch(input, temp, Console.WriteLine);
                    if (MainForm.ReadPclVersion(temp) != version)
                        throw new InvalidDataException("补丁输出版本不一致，未替换原文件。");
                    File.Replace(temp, input, null, true);
                    temp = null;
                    Console.WriteLine("[完成] 补丁成功，原文件已直接替换");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[失败] " + ex.Message);
                return 2;
            }
            finally
            {
                if (temp != null)
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            if (!new AssemblyName(args.Name).Name.Equals("Mono.Cecil", StringComparison.OrdinalIgnoreCase))
                return null;

            string dependencyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mono.Cecil.dll");
            if (File.Exists(dependencyPath)) return Assembly.LoadFrom(dependencyPath);

            Assembly assembly = typeof(Program).Assembly;
            string resourceName = null;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith("Mono.Cecil.dll", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }
            if (resourceName == null) return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                return Assembly.Load(bytes);
            }
        }
    }
}
