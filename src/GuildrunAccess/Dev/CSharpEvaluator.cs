using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace GuildrunAccess.Dev
{
    /// <summary>
    /// Wraps the Roslyn scripting API so the dev driver can POST arbitrary C# and run it against the live
    /// game. State persists across calls (usings, variables defined in one eval are visible to the next),
    /// so the session behaves like a REPL. Evaluated code can reference any loaded, disk-backed assembly,
    /// including the Il2Cpp game proxies. Byte-loaded assemblies (Core, Module) have no Location and are
    /// not referenceable; reach the reloadable side through the dev endpoints instead.
    ///
    /// MUST be used from the Unity main thread: evaluated code routinely touches Unity objects.
    /// </summary>
    internal sealed class CSharpEvaluator
    {
        private ScriptState<object> _state;
        private ScriptOptions _options;

        /// <summary>Drop REPL state so the next Eval rebuilds it.</summary>
        public void Reset()
        {
            _state = null;
            _options = null;
        }

        private void Initialize()
        {
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;
                string location;
                try { location = asm.Location; }
                catch { continue; }
                if (string.IsNullOrEmpty(location) || !seen.Add(location))
                    continue;
                try { refs.Add(MetadataReference.CreateFromFile(location)); }
                catch { }
            }

            // Also reference every interop proxy by file: the loaded-assembly sweep above runs at the
            // first eval, before most proxies have lazily loaded.
            string interop = Path.Combine(BepInEx.Paths.BepInExRootPath, "interop");
            if (Directory.Exists(interop))
            {
                foreach (string dll in Directory.GetFiles(interop, "*.dll"))
                {
                    if (!seen.Add(dll))
                        continue;
                    try { refs.Add(MetadataReference.CreateFromFile(dll)); }
                    catch { }
                }
            }

            _options = ScriptOptions.Default
                .WithReferences(refs)
                .WithImports("System", "System.Linq", "System.Reflection",
                    "System.Collections.Generic", "UnityEngine");
        }

        /// <summary>Compile a bool expression into a reusable delegate for the /wait endpoint, in the
        /// SAME session as /eval. Returns null on success with <paramref name="predicate"/> set, else
        /// the compile/exception text.</summary>
        public string CompilePredicate(string expression, out Func<bool> predicate)
        {
            predicate = null;
            if (_options == null)
                Initialize();
            try
            {
                _state = _state == null
                    ? CSharpScript.RunAsync("(System.Func<bool>)(() => (" + expression + "))", _options).GetAwaiter().GetResult()
                    : _state.ContinueWithAsync("(System.Func<bool>)(() => (" + expression + "))", _options).GetAwaiter().GetResult();
                predicate = _state.ReturnValue as Func<bool>;
                return predicate != null ? null : "[error] expression did not yield a bool predicate\n";
            }
            catch (CompilationErrorException e)
            {
                return "[compile] " + string.Join("\n", e.Diagnostics) + "\n";
            }
            catch (Exception e)
            {
                return "[exception] " + e + "\n";
            }
        }

        /// <summary>Compile and run <paramref name="code"/>; return output + result/errors.</summary>
        public string Eval(string code)
        {
            if (_options == null)
                Initialize();

            var output = new StringWriter();
            TextWriter origOut = Console.Out;
            TextWriter origErr = Console.Error;

            object value = null;
            bool hasValue = false;
            Exception thrown = null;

            Console.SetOut(output);
            Console.SetError(output);
            try
            {
                _state = _state == null
                    ? CSharpScript.RunAsync(code, _options).GetAwaiter().GetResult()
                    : _state.ContinueWithAsync(code, _options).GetAwaiter().GetResult();
                if (_state.ReturnValue != null)
                {
                    value = _state.ReturnValue;
                    hasValue = true;
                }
            }
            catch (Exception e)
            {
                thrown = e;
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }

            var sb = new StringBuilder();
            string captured = output.ToString();
            if (captured.Length > 0)
            {
                sb.Append(captured);
                if (!captured.EndsWith("\n"))
                    sb.Append('\n');
            }
            if (thrown is CompilationErrorException compileError)
                sb.Append("[compile] ").Append(string.Join("\n", compileError.Diagnostics)).Append('\n');
            else if (thrown != null)
                sb.Append("[exception] ").Append(thrown).Append('\n');
            if (hasValue)
                sb.Append("=> ").Append(value).Append('\n');
            if (sb.Length == 0)
                sb.Append("(ok)\n");
            return sb.ToString();
        }
    }
}
