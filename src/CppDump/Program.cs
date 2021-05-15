using CppAst;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CppDump
{
    #region PropsContractResolver
    public class PropsContractResolver : DefaultContractResolver
    {
        public HashSet<String> ignoreProps { get; set; } = new HashSet<String> { };
        public Dictionary<String, HashSet<String>> typePropsMap { get; set; } = new Dictionary<String, HashSet<String>> { };

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> list = base.CreateProperties(type, memberSerialization);
            if (typePropsMap.ContainsKey(type.FullName))
            {
                HashSet<String> props = typePropsMap[type.FullName];
                return list.Where(p => props.Contains(p.PropertyName)).ToList();
            }
            return list.Where(p => !ignoreProps.Contains(p.PropertyName)).ToList(); ;
        }
    }
    #endregion

    #region AstContainer
    //
    // 摘要:
    //     A base Cpp container for macros, classes, fields, functions, enums, typesdefs.
    public class DumpAstContainer
    {
        //
        public List<CppAttribute> Attributes { get; set; }
        //
        public List<CppClass> Classes { get; set; }
        //
        public List<CppEnum> Enums { get; set; }
        //
        public List<CppField> Fields { get; set; }
        //
        public List<CppFunction> Functions { get; set; }
        //
        public List<CppTypedef> Typedefs { get; set; }

        public List<CppMacro> Macros { get; set; }

        public List<CppNamespace> Namespaces { get; set; }

        public Dictionary<String, List<ICppDeclaration>> FuncMap { get; }

        public DumpAstContainer(CppCompilation c)
        {
            Attributes = c.Attributes.ToList();
            Classes = c.Classes.ToList();
            Enums = c.Enums.ToList();
            Fields = c.Fields.ToList();
            Functions = c.Functions.ToList();
            Typedefs = c.Typedefs.ToList();
            Macros = c.Macros.ToList();
            Namespaces = c.Namespaces.ToList();

            FuncMap = new Dictionary<String, List<ICppDeclaration>>();
            foreach (CppFunction f in Functions)
            {
                var tmp = f.Children().Where(p => typeof(CppParameter) != p.GetType());
                if (tmp != null && tmp.Count() > 0 && f.Name != null && f.Name != "")
                {
                    FuncMap.Add(f.Name, tmp.ToList());
                }
            }
        }

        static JsonSerializerSettings JsonConfig(PropsContractResolver r)
        {
            JsonSerializerSettings jSetting = new JsonSerializerSettings();

            jSetting.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            jSetting.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            jSetting.ContractResolver = r;
            return jSetting;
        }

        public static void PrintAst(CppCompilation compilation, Boolean printWarn = false, Boolean printInfo = false)
        {
            // Print diagnostic messages
            foreach (var message in compilation.Diagnostics.Messages)
            {
                if (printWarn)
                {
                    Console.WriteLine(message);
                }
                else if (message.Type == CppLogMessageType.Error)
                {
                    Console.WriteLine(message);
                }
            }

            if (printInfo)
            {
                // Print All enums
                foreach (var cppEnum in compilation.Enums)
                    Console.WriteLine(cppEnum);

                // Print All functions
                foreach (var cppFunction in compilation.Functions)
                    Console.WriteLine(cppFunction);

                // Print All classes, structs
                foreach (var cppClass in compilation.Classes)
                    Console.WriteLine(cppClass);

                // Print All typedefs
                foreach (var cppTypedef in compilation.Typedefs)
                    Console.WriteLine(cppTypedef);
            }
        }

        public void DumpAsJson(StreamWriter wf)
        {
            var jsonSerializer = JsonSerializer.Create(JsonConfig(new PropsContractResolver()
            {
                typePropsMap = {
                    [typeof(CppEnum).FullName] = new HashSet<String>{ "Name", "SizeOf", "TypeKind", "Items", "Span" },
                    [typeof(CppEnumItem).FullName] = new HashSet<String>{ "Name", "Value" },

                    [typeof(CppClass).FullName] = new HashSet<String>{ "Name", "SizeOf", "Fields", "ClassKind", "Typedefs ", "Span" },
                    [typeof(CppField).FullName] = new HashSet<String>{ "Name", "Type", "BitFieldWidth", "StorageQualifier", "InitValue" },

                    [typeof(CppType).FullName] = new HashSet<String>{ "Kind", "SizeOf", "TypeKind" },

                    [typeof(CppTypedef).FullName] = new HashSet<String>{ "Name", "SizeOf", "TypeKind", "ElementType" },
                    [typeof(CppPointerType).FullName] = new HashSet<String>{ "Kind", "SizeOf", "TypeKind", "ElementType" },

                    [typeof(CppArrayType).FullName] = new HashSet<String>{ "Kind", "Size", "SizeOf", "TypeKind", "ElementType" },

                    [typeof(CppFunctionType).FullName] = new HashSet<String>{ "Kind", "TypeKind", "ReturnType", "Parameters" },
                    [typeof(CppParameter).FullName] = new HashSet<String>{ "Name", "Type" },

                    [typeof(CppFunction).FullName] = new HashSet<String>{ "Name", "Flags", "LinkageKind", "LinkageKind", "Parameters", "ReturnType", "StorageQualifier" },
                },
                ignoreProps = { "Parent", "Span", "Comment", "Visibility" },
            }));
            jsonSerializer.Serialize(wf, this);
        }

    }
    #endregion

    #region ClConfig
    class ClConfig
    {
        public String cmd { get; set; }
        public List<String> include { get; set; }
        public List<String> input { get; set; }
        public List<String> flag { get; set; }
        public List<String> zc { get; set; }
        public Dictionary<String, String> define { get; set; }
        public Dictionary<String, String> file { get; set; }
        public Dictionary<String, String> warn { get; set; }

        public ClConfig FixCfg(String cwd)
        {
            this.input = this.input.Select(p => Path.Combine(cwd, p)).ToList();
            this.include = this.include.Select(p => Path.Combine(cwd, p)).ToList();
            return this;
        }

        public ClConfig Copy()
        {
            ClConfig tmp = (ClConfig)this.MemberwiseClone();
            tmp.cmd = this.cmd;
            tmp.include = this.include.ToList();
            tmp.input = this.input.ToList();
            tmp.flag = this.flag.ToList();
            tmp.zc = this.zc.ToList();

            tmp.define = this.define.ToDictionary(kv => kv.Key, kv => kv.Value);
            tmp.file = this.file.ToDictionary(kv => kv.Key, kv => kv.Value);
            tmp.warn = this.warn.ToDictionary(kv => kv.Key, kv => kv.Value);
            return tmp;
        }
    }
    #endregion

    class Program
    {
        static bool _ONLY_TEST = false;
        static bool _USE_IPP = false;

        static String clJsonFile = @"D:\php_sdk\phpdev\vc15\x64\tcc\p-parser\nmake_cl.json";
        static String srcDir = @"D:\php_sdk\phpdev\vc15\x64\php-src";
        static String objDir = @"D:\php_sdk\phpdev\vc15\x64\tcc\p-parser\obj";

        #region test string
        static String testJsonStr = @"
[
  {
    'cmd': 'cl.exe', 
    'zc': [
      'inline',
      '__cplusplus',
      'wchar_t'
    ], 
    'warn': {
      '4996': 'd'
    }, 
    'flag': [
      'nologo',
      'W3',
      'FD',
      'd2FuncCache1',
      'MP',
      'LD',
      'MD',
      'W3',
      'Ox',
      'GF',
      'c'
    ], 
    'file': {
      'p': 'D:\\php_sdk\\phpdev\\vc15\\x64\\php-src\\x64\\Release\\sapi\\cli\\', 
      'R': 'D:\\php_sdk\\phpdev\\vc15\\x64\\php-src\\x64\\Release\\sapi\\cli\\', 
      'd': 'D:\\php_sdk\\phpdev\\vc15\\x64\\php-src\\x64\\Release\\sapi\\cli\\', 
      'o': 'D:\\php_sdk\\phpdev\\vc15\\x64\\php-src\\x64\\Release\\sapi\\cli\\'
    }, 
    'input': [
      'sapi\\cli\\php_cli.c',
      'sapi\\cli\\php_cli_process_title.c',
      'sapi\\cli\\php_cli_server.c',
      'sapi\\cli\\php_http_parser.c',
      'sapi\\cli\\ps_title.c'
    ], 
    'include': [
      'D:\\php_sdk\\phpdev\\vc15\\x64\\deps\\include',
      '.',
      'main',
      'Zend',
      'TSRM',
      'ext',
      'D:\\php_sdk\\phpdev\\vc15\\x64\\deps\\include'
    ], 
    'define': {
      'ZEND_WIN32_FORCE_INLINE': null, 
      'NDEBUG': null, 
      'NDebug': null, 
      'PHP_WIN32': '1', 
      'HAVE_LIBEDIT': null, 
      'WINDOWS': '1', 
      'HAVE_EDITLINE_READLINE_H': '1', 
      'WIN32': null, 
      'ZEND_DEBUG': '0', 
      '_USE_MATH_DEFINES': null, 
      'ZEND_ENABLE_STATIC_TSRMLS_CACHE': '1', 
      '_MBCS': null, 
      'FD_SETSIZE': '256', 
      'ZEND_WIN32': '1', 
      '_WINDOWS': null
    }
  }
]
".Replace("'", "\"");
        #endregion

        #region util functions
        static String FixObjDir(String file, String cwd, String ext)
        {
            var arr = file.Split('\\');
            String f = arr[arr.Count() - 1];
            var fArr = f.Split('.');
            fArr[fArr.Count() - 1] = ext;
            String objFile = String.Join(".", fArr);
            return Path.Combine(cwd, objFile);
        }

        static String ReadFile(String rf)
        {
            using (FileStream fsRead = new FileStream(rf, FileMode.Open))
            {
                int fsLen = (int)fsRead.Length;
                byte[] heByte = new byte[fsLen];
                int r = fsRead.Read(heByte, 0, heByte.Length);
                string myStr = System.Text.Encoding.UTF8.GetString(heByte);
                return myStr;
            }
        }

        static void WriteFile(String wf, String str)
        {
            byte[] myByte = System.Text.Encoding.UTF8.GetBytes(str);
            using (FileStream fsWrite = new FileStream(wf, FileMode.OpenOrCreate))
            {
                fsWrite.Write(myByte, 0, myByte.Length);
            };
        }

        static Stopwatch stopWatch = new Stopwatch();

        static List<ClConfig> SplitCfgByInput(List<ClConfig> cfgs, List<String> sInput)
        {
            List<ClConfig> appends = new List<ClConfig>();

            foreach (ClConfig cfg in cfgs)
            {
                foreach (String input in sInput)
                {
                    if (cfg.input.Contains(input))
                    {
                        ClConfig tmp = cfg.Copy();
                        tmp.input = new List<String> { input };
                        appends.Add(tmp);

                        cfg.input = cfg.input.Except(tmp.input).ToList();
                    }
                }
            }
            return cfgs.Concat(appends).ToList();
        }

        #endregion

        static void Main(string[] args)
        {
            // https://github.com/xoofx/CppAst
            // https://github.com/xoofx/CppAst/blob/master/doc/readme.md

            #region cfgs
            List<ClConfig> cfgs;
            if (_ONLY_TEST)
            {
                cfgs = JsonConvert.DeserializeObject<List<ClConfig>>(testJsonStr);
            }
            else
            {
                cfgs = JsonConvert.DeserializeObject<List<ClConfig>>(ReadFile(clJsonFile));
            }
            /* cfgs = SplitCfgByInput(cfgs, new List<String>{
                "Zend\\zend_API.c",
                "Zend\\zend_language_parser.c",
                "Zend\\zend_language_scanner.c",
                "main\\streams\\memory.c",
                "ext\\date\\lib\\parse_iso_intervals.c",
                "ext\\date\\lib\\unixtime2tm.c",
                "ext\\hash\\hash_md.c",
                "ext\\hash\\hash_ripemd.c",
                "ext\\hash\\hash_sha.c",
                "ext\\hash\\hash_snefru.c",
                "ext\\pcre\\pcre2lib\\pcre2_convert.c",
                "ext\\standard\\crypt_sha512.c",
                "ext\\standard\\dir.c",
                "ext\\standard\\file.c",
                "ext\\standard\\image.c",
                "ext\\standard\\php_crypt_r.c",
                "ext\\standard\\url.c",
                "ext\\standard\\url_scanner_ex.c",
                "ext\\standard\\var.c",
            }); */
            #endregion

            objDir = _USE_IPP ? (objDir + "_i") : objDir;

            foreach (ClConfig cfg in cfgs)
            {
                var fixCfg = cfg.FixCfg(srcDir);
                foreach (String _file in fixCfg.input)
                {
                    String file = _USE_IPP ? (_file + ".ipp") : _file;

                    String outFile = FixObjDir(file, objDir, "obj");
                    if (File.Exists(outFile))
                    {
                        // Console.WriteLine("parse skip \t\t" + file);
                        continue;
                    }

                    Console.WriteLine("parse strat \t\t" + file);

                    stopWatch.Reset();
                    stopWatch.Start();
                    CppCompilation compilation = Do(file, fixCfg);
                    stopWatch.Stop();

                    long used = stopWatch.ElapsedMilliseconds;
                    Console.WriteLine("parse end \t\t" + file + " use: " + used + "ms");

                    DumpAstContainer.PrintAst(compilation);

                    if (!compilation.HasErrors)
                    {
                        DumpAstContainer astCon = new DumpAstContainer(compilation);

                        Console.WriteLine("dump strat \t\t" + file + " => " + outFile);

                        stopWatch.Reset();
                        stopWatch.Start();
                        using (StreamWriter wf = new StreamWriter(outFile, false))
                        {
                            astCon.DumpAsJson(wf);
                        };
                        stopWatch.Stop();

                        long usedDump = stopWatch.ElapsedMilliseconds;
                        Console.WriteLine("dump end \t\t" + outFile + " use: " + usedDump + "ms");
                    }
                }
            }

            Console.Write("\nPlease press 'Enter' key to continue ... ");
            Console.ReadLine();
        }

        static CppCompilation Do(String file, ClConfig cfg)
        {
            var options = new CppParserOptions()
            {
                // Pass the defines -DMYDEFINE to the C++ parser
                Defines = { },
                IncludeFolders = { },
                TargetCpu = CppTargetCpu.X86_64,
            };
            options.ParseSystemIncludes = false;
            options.ParseComments = false;
            options.AdditionalArguments.Add("-fno-complete-member-pointers");
            options.AdditionalArguments.Add("/TC");
            options.AdditionalArguments.Add("-std c99");
            options.AdditionalArguments.Add("/c");
            options.AdditionalArguments.Add("-fms-extensions");
            options.AdditionalArguments.Add("-fms-compatibility");
            options.AdditionalArguments.Add(" -fms-compatibility-version=19");
            options.ParseAsCpp = false;

            options.EnableFunctionBodies();

            foreach (KeyValuePair<string, string> kv in cfg.define)
            {
                String d = kv.Value == null ? (kv.Key) : (kv.Key + "=" + kv.Value);
                options.Defines.Add(d);
            }
            options.Defines.Add("CPP_AST_FIXED");

            foreach (String include in cfg.include)
            {
                options.IncludeFolders.Add(include);
            }

            options.ConfigureForWindowsMsvc();

            var compilation = CppParser.ParseFile(file, options);
            return compilation;
        }
    }
}
