using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ParallelNUnit
{
    class Program
    {
        private static readonly List<NUnitRun> Running = new List<NUnitRun>();

        private static int Main(string[] args)
        {
            int returnCode = MainImpl(args);
            Log("Exiting with code: {0}", returnCode);

            return returnCode;
        }

        private static int MainImpl(string[] args)
        {
            Log("Started with arguments: {0}", String.Join(Environment.NewLine, args));

            try
            {
                CmdOptions cmdOptions = CmdOptions.Parse(args);

                List<string> errors = cmdOptions.Validate();
                if (errors.Count > 0)
                {
                    Log("Failed, arguments are invalid - {0}", String.Join(Environment.NewLine, errors));
                    return ExitCode.InvalidArguments;
                }

                var batches = cmdOptions.Assemblies.SelectMany(assemblyPath => GenerateBatches(assemblyPath, cmdOptions.SplitByCategory));

                if (cmdOptions.DryRun)
                {
                    foreach (var batch in batches)
                    {
                        Log(batch.GetNunitCmdLine());
                    }
                    return ExitCode.Success;
                }

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = cmdOptions.MaxDegreeOfParallelism
                };

                try
                {
                    Parallel.ForEach(batches, parallelOptions, batch => RunNunit(cmdOptions, batch));
                }
                catch (OperationCanceledException ex)
                {
                    Log(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Log("Failed, Unknown error - {0}", ex);
                return ExitCode.UnknownError;
            }

            Log("[ParallelNUnit]: Completed successfully.");

            return ExitCode.Success;
        }

        private static IEnumerable<NUnitBatch> GenerateBatches(string assemblyPath, bool splitByCategory)
        {
            if (!splitByCategory)
            {
                return new[] {new NUnitBatch {AssemblyPath = assemblyPath}};
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                var categories = (from type in assembly.GetTypes()
                    from attribute in type.GetCustomAttributes(typeof (CategoryAttribute), false)
                    let catAttrib = attribute as CategoryAttribute
                    select catAttrib.Name).Distinct().ToList();
                
                List<NUnitBatch> batches = categories
                    .Select(category => new NUnitBatch {AssemblyPath = assemblyPath, Include = category})
                    .ToList();

                string excludeAll = string.Join(",", categories);

                batches.Add(new NUnitBatch{AssemblyPath = assemblyPath, Exclude  = excludeAll});

                return batches;
            }
            catch (Exception ex)
            {
                Log("Failed to split assembly {0} in to categories: {1}", assemblyPath, ex);
                return new[] {new NUnitBatch {AssemblyPath = assemblyPath}};
            }
        }

        private static void RunNunit(CmdOptions cmdOptions, NUnitBatch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException("batch");
            }

            try
            {
                Log("Processing {0}", batch);

                if (cmdOptions.DryRun)
                {
                    return;
                }

                var sw = new Stopwatch();
                sw.Start();

                var pinfo = new ProcessStartInfo(cmdOptions.NunitConsoleRunnerPath)
                {
                    Arguments = batch.GetNunitCmdLine(),
                    UseShellExecute = false
                };

                Process process = Process.Start(pinfo);

                Running.Add(new NUnitRun {Process = process, StartInfo = pinfo});

                process.WaitForExit();

                Log("Processed {0}. Duration: {1} ExitCode: {2}", batch, sw.Elapsed, process.ExitCode);
            }
            catch (Exception ex)
            {
                Log("Failed to process {0}.", batch, ex);
            }
        }

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine("[ParalleNUnit] " + format, args);
        }
    }

    internal class NUnitBatch
    {
        public string AssemblyPath { get; set; }

        //Category
        public string Include { get; set; }
        public string Exclude { get; set; }

        public string GetNunitCmdLine()
        {
            var sb = new StringBuilder();
            
            if (!string.IsNullOrWhiteSpace(Include))
            {
                sb.AppendFormat("/include:{0} ", Include);    
            }
            else if(!string.IsNullOrWhiteSpace(Exclude))
            {
                sb.AppendFormat("/exclude:{0} ", Exclude);    
            }
            
            sb.AppendFormat(AssemblyPath);
            return sb.ToString();
        }

        public override string ToString()
        {
            return GetNunitCmdLine();
        }
    }
}
