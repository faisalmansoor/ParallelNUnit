using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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

                var batches = cmdOptions.Assemblies.Select(assemblyPath => new NUnitBatch {AssemblyPath = assemblyPath});

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = cmdOptions.MaxDegreeOfParallelism
                };

                try
                {
                    Parallel.ForEach(batches, parallelOptions, batch => RunNunit(cmdOptions.NunitConsoleRunnerPath, batch));
                }
                catch (OperationCanceledException ex)
                {
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

        private static void RunNunit(string nunitPath, NUnitBatch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException("batch");
            }

            try
            {
                Log("Processing {0}", batch);

                var sw = new Stopwatch();
                sw.Start();


                var pinfo = new ProcessStartInfo(nunitPath)
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
        public string Fixture { get; set; }

        public string GetNunitCmdLine()
        {
            return AssemblyPath;
//            var sb = new StringBuilder();
//            sb.AppendFormat("/xml={0}-out.xml ", AssemblyPath);
//
//            if (!string.IsNullOrWhiteSpace(Fixture))
//            {
//                sb.AppendFormat("/fixture={0} ", Fixture);    
//            }
//            
//            sb.AppendFormat(AssemblyPath);
//            return sb.ToString();
        }

        public override string ToString()
        {
            return String.Format("Assembly: {0}", AssemblyPath);
        }
    }
}
