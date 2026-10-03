using System;
using System.IO;
using System.Text;

namespace WitchTower.Save
{
    // Fixed public deployments: no server-supplied paths, copying, migration or
    // credential lookup. The review account is created later by the existing
    // repository in a new namespace. Production keeps its existing directory.
    internal static class ReleasePurchaseStorage
    {
        private const string Marker = "purchase-environment-v1.txt";

        internal static string Resolve(string persistentRoot, string environment)
        {
            if ((environment != "Production" && environment != "Sandbox") ||
                string.IsNullOrEmpty(persistentRoot) || !Path.IsPathRooted(persistentRoot) ||
                Path.GetFullPath(persistentRoot) != persistentRoot ||
                persistentRoot == Path.GetPathRoot(persistentRoot))
                throw new InvalidOperationException("Invalid release storage environment.");
            CheckDirectory(persistentRoot);
            string parent = Path.Combine(persistentRoot, "environments");
            CheckDirectory(parent);
            string root = Path.Combine(parent, environment == "Production" ? "production" : "review-sandbox-v1");
            CheckDirectory(root);
            string marker = Path.Combine(root, Marker);
            string temporary = marker + ".tmp";
            string expected = "1\n" + environment + "\n" +
                (environment == "Production" ? AppleAccountConfiguration.ProductionEndpoint
                    : AppleAccountConfiguration.ReviewSandboxEndpoint) + "\n";
            CheckFile(marker); CheckFile(temporary);
            if (File.Exists(marker))
            {
                if (File.ReadAllText(marker) != expected)
                    throw new IOException("Release storage binding mismatch.");
            }
            else
            {
                // Only the established production directory may contain legacy
                // data. Never adopt an unmarked, populated review directory.
                if (environment == "Sandbox" && Directory.Exists(root))
                    foreach (string entry in Directory.GetFileSystemEntries(root))
                        if (entry != temporary) throw new IOException("Unbound review storage.");
                Directory.CreateDirectory(root);
                if (File.Exists(temporary))
                {
                    if (File.ReadAllText(temporary) != expected)
                        throw new IOException("Incomplete release storage binding mismatch.");
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(expected);
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                }
                File.Move(temporary, marker);
            }
            return root;
        }

        private static void CheckDirectory(string path)
        {
            if (File.Exists(path)) throw new IOException("File at release directory path.");
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked release storage is not supported.");
        }
        private static void CheckFile(string path)
        {
            if (Directory.Exists(path)) throw new IOException("Directory at release binding path.");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked release binding is not supported.");
        }
    }
}
