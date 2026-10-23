using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PclDownloadPatch
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(AppDomain.CurrentDomain.BaseDirectory));
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
