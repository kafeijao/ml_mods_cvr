﻿using System;
using System.IO;
using System.Reflection;

namespace ml_asl
{
    static class ResourcesHandler
    {
        readonly static string ms_namespace = typeof(ResourcesHandler).Namespace;

        public static string GetEmbeddedResource(string p_name)
        {
            string l_result = "";
            Assembly l_assembly = Assembly.GetExecutingAssembly();

            try
            {
                Stream l_libraryStream = l_assembly.GetManifestResourceStream(ms_namespace + ".resources." + p_name);
                StreamReader l_streadReader = new StreamReader(l_libraryStream);
                l_result = l_streadReader.ReadToEnd();
            }
            catch(Exception) { }

            return l_result;
        }
    }
}
