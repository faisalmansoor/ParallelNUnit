using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NDesk.Options;

namespace ParallelNUnit
{
    public class CmdOptions
    {
        public CmdOptions()
        {
            Assemblies = new List<string>();
            MaxDegreeOfParallelism = 4;
        }

        public bool Verbose { get; private set; }
        public string NunitConsoleRunnerPath { get; private set; }
        public List<string> Assemblies { get; private set; }
        public int MaxDegreeOfParallelism { get; private set; }
        
        public static CmdOptions Parse(string[] args)
        {
            var options = new CmdOptions();

            OptionSet p = new OptionSet()
                .Add("v|verbose", v => { if (v != null) options.Verbose = true; })
                .Add("n=|nunit=", v => { if (v != null) options.NunitConsoleRunnerPath = v; })
                .Add("d=|degree=", v =>
                {
                    int degree;
                    if (int.TryParse(v, out degree))
                    {
                        options.MaxDegreeOfParallelism = degree;
                    } 
                })
                .Add("<>", v => options.Assemblies.Add(v));

            p.Parse(args);

            return options;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (!File.Exists(NunitConsoleRunnerPath))
            {
                errors.Add(String.Format("Cannot find nunit console runner file {0}", NunitConsoleRunnerPath));
            }

            return errors;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Verbose = {0}\n", Verbose);
            sb.AppendFormat("NunitConsoleRunnerPath = {0}\n", NunitConsoleRunnerPath);
            sb.AppendFormat("MaxDegreeOfParallelism = {0}\n", MaxDegreeOfParallelism);
            sb.AppendFormat("Assemblies = {0}\n", String.Join(Environment.NewLine, Assemblies));
            return sb.ToString();
        }
    }
}