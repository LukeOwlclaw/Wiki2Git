using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;

// Ignore spelling: bool namespace foreach mc ok
namespace CLAParser
{
    /// <summary>
    /// <para>Class CLAParser provides everything for easy and fast handling of command-line arguments.</para>
    /// <para>Required and optional parameters can be defined, as well as the type of each parameter (e.g. bool, int, string).</para>
    /// <para>If command-line is correct, all arguments and their values can be accessed via a Dictionary interface.</para>
    /// <para>In case of an error, an exception is raised which explains the error.</para>
    /// <para>All output text can be nationalized by means of a ResourceManager.</para>
    /// </summary>
    internal class CmdLineArgumentParser : IEnumerable, IEnumerator
    {
        /// <summary>
        /// On parsing all parameters of command line are stored here
        /// if they match the requirements defined in WanntedParameters.
        /// </summary>
        private StringDictionary mFoundParameters;
        private IEnumerator mEnumerator;
        private readonly ResourceManager mCmdLineArgResourceManager;

        /// <summary>
        /// Stores all available information about a parameter defined by the programmer.
        /// </summary>
        private class ParameterDefintion
        {
            public ParameterDefintion(string parameterName, ParamAllowType allowType, ValueType valueType, string parameterHelp)
            {
                Parameter = parameterName;
                AllowType = allowType;
                ValueType = valueType;
                Help = parameterHelp;
            }

            public string Parameter { get; set; }
            public ParamAllowType AllowType { get; set; }
            public ValueType ValueType { get; set; }
            public string Help { get; set; }
        }

        /// <summary>
        /// Collection of all optional and required parameters. This is the base for validation.
        /// </summary>
        private readonly SortedDictionary<string, ParameterDefintion> mWantedParameters;

        /// <summary>
        /// Defines the type of possible command-line arguments.
        /// Note all arguments can either start with / or with -.
        /// </summary>
        public enum ValueType
        {
            /// <summary>
            /// <para>Parameter must be followed by a string,</para>
            /// examples: /p string, /p two string, /p "enclosed by quotes", /p "enclosed by single quotes"
            /// </summary>
            String,

            /// <summary>
            /// <para>Parameter can be followed by a string,</para>
            /// examples: /p, /p string
            /// </summary>
            OptionalString,

            /// <summary>
            /// <para>Parameter must be following be a whole number.</para>
            /// Examples: /p 12, /p -100
            /// </summary>
            Int,

            /// <summary>
            /// <para>Parameter can be followed by a whole number.</para>
            /// <para>If no number is specified it will be interpreted as 0.</para>
            /// Examples: /p, /p 12, /p "-100"
            /// </summary>
            OptionalInt,

            /// <summary>
            /// <para>Parameter is a switch. It can be defined or not.</para>
            /// <para>Note: If AllowType is set to Required, this parameter kind does not make much sense.</para>
            /// Examples: program.exe, program.exe /p
            /// </summary>
            Bool,

            /// <summary>
            /// <para>Parameter can be used many times, however they must not be a value defined.</para>
            /// <para>The number of times of occurrences is count (/p /p /p will return "3").</para>
            /// <para>Note: If AllowType is set to Required, switch must appear at least once.</para>
            /// Examples: program.exe, program.exe /p, program.exe /p /p /p /p
            /// </summary>
            MultipleBool,

            /// <summary>
            /// <para>Parameter can be followed by several whole numbers.</para>
            /// <para>If AllowType is set to Required, at least one number must be specified.</para>
            /// <para>Note: Including a negative number requires quoting the series of numbers.</para>
            /// Examples: /p 1, /p 1 2 3, /p "0 1 -1 2 -2"
            /// </summary>
            MultipleInts,
        }

        /// <summary>
        /// Specifies whether the parameter has to be supplied or whether it is optional.
        /// </summary>
        public enum ParamAllowType
        {
            /// <summary>
            /// Parameter is optional
            /// </summary>
            Optional,

            /// <summary>
            /// Parameter is required
            /// </summary>
            Required,
        }

        /// <summary>
        /// <para>Additional parameters are those not defined by the programmers using the Parameter() function.</para>
        /// <para>By default this variable is false, thus causing an exception if the user specifies an undefined parameter,</para>
        /// <para>e.g. program.exe /undefined_parameter value_5</para>
        /// </summary>
        public bool AllowAdditionalParameters { get; set; }

        /// <summary>
        /// <para>ParameterPrefix specifies the prefix for all parameters. It is just used by</para>
        /// <para>GetUsage() and GetParameterInfo(), not the Parse(). [Parse always accepts / and -]</para>
        /// <para>By default this variable is set to /</para>
        /// </summary>
        public string ParameterPrefix { get; set; }

        /// <summary>
        /// <para>Retrieve a parameter value if it exists.</para>
        /// <para>Remember: program.exe /parameter value.</para>
        /// <para>Note: Function Parse() must be called before.</para>
        /// </summary>
        /// <param name="param">Specifies the parameter</param>
        /// <returns>The corresponding value, or null if the is no such parameter,
        /// or if Parse() was not called yet.</returns>
        public string? this[string param]
        {
            get
            {
                if (mFoundParameters == null) { return null; }
                else { return mFoundParameters[param]; }
            }
        }

        /// <summary>
        /// <para>Class CLAParser provides everything for easy and fast handling of command-line arguments.</para>
        /// <para>Usage:</para>
        /// <para>1) Create an instance of CLAParser by calling this constructor.</para>
        /// <para>2) Define parameters by calling Parameter() as often as needed.</para>
        /// <para>3) Optionally: Set variables such as AllowAdditionalParameters and ParameterPrefix.</para>
        /// <para>4) Call Parse(), catch all CmdLineArgumentExceptions, and show those to user.</para>
        /// <para>5) Call GetUsage() and GetParameterInfo() to create information about using command-line arguments.</para>
        /// </summary>
        /// <param name="namespaceOfResX">Pass the name of the default namespace (usually the namespace of main code file Program.cs)<para>[This is necessary so that CLAParser can find its resource files (CmdLineArgumentParserRes.resx, CmdLineArgumentParserRes.de-DE.resx, ...)]</para></param>
        public CmdLineArgumentParser(string namespaceOfResX)
        {
            mCmdLineArgResourceManager = new ResourceManager(namespaceOfResX + ".CmdLineArgumentParserRes", GetType().Assembly);

            mFoundParameters = new StringDictionary();
            mWantedParameters = new SortedDictionary<string, ParameterDefintion>(StringComparer.InvariantCultureIgnoreCase);
            mEnumerator = mFoundParameters.GetEnumerator();

            ParameterPrefix = "/";
            AllowAdditionalParameters = false;
        }

        /// <summary>
        /// <para>Defines parameters which program understands.</para>
        /// <para>Parameter() can be called as often as required.</para>
        /// <para>Information passed to CLAParser by Parameter() is later used by Parse(), GetUsage(), GetParamaterInfo()</para>
        /// </summary>
        /// <param name="allowType">Choose parameter to be either as optional or required.</param>
        /// <param name="parameterName">Name of the parameter (everything behind / )</param>
        /// <param name="valueType">Defines valid values for the parameter.</param>
        /// <param name="parameterHelp">Information about the parameter. This string will later be used by GetParameterInfo().</param>
        public void Parameter(ParamAllowType allowType, string parameterName, ValueType valueType, string parameterHelp)
        {
            //for the first value without parameter name only type string is accepted.
            //this is supposed to be a development exception which needs no i18n.
            if (string.IsNullOrEmpty(parameterName) && valueType != ValueType.String)
            {
                throw new Exception("For the first value (without parameter name) only type ValueType.String is accepted! ");
            }

            ParameterDefintion param = new ParameterDefintion(parameterName, allowType, valueType, parameterHelp);
            mWantedParameters.Add(param.Parameter, param);
        }

        /// <summary>
        /// <para>Starts the parsing process. Throws CmdLineArgumentExceptions in case of errors.</para>
        /// <para>Afterwards use the enumerator or the dictionary interface to access the found parameters and their values.</para>
        /// </summary>
        /// <param name="argumentLine">Argument line passed via command line to the program.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public void Parse(string argumentLine)
        {
            mFoundParameters = new StringDictionary();
            mEnumerator = mFoundParameters.GetEnumerator();

            //pure: ^[\s]*((?<UnknownValues>("[^"]*")|('[^']*')|([^ "'/-]*)?)[\s]*)*([\s]*[/-](?<name>[^\s-/:=]+)([:=]?)([\s]*)(?<value>("[^"]*")|('[^']*')|([\s]*[^/-][^\s]+[\s]*)|([^/-]+)|)?([\s]*))*$
            string correctCmdLineRegEx = "^[\\s]*((?<UnknownValues>(\"[^\"]*\")|('[^']*')|([^ \"'/-]*)?)[\\s]*)*([\\s]*(?<prefix>[/-])(?<name>[^\\s-/:=]+)([:=]?)([\\s]*)(?<value>(\"[^\"]*\")|('[^']*')|([\\s]*[^/-][^\\s]+[\\s]*)|([^/-]+)|)?([\\s]*))*$";
            string paramValuePairRegEx = "^(([\\s]*[/-])(?<name>[^\\s-/:=]+)([:=]?)([\\s]*)(?<value>(\"[^\"]*\")|('[^']*')|([\\s]*[^/-][^\\s]+[\\s]*)|([^/-]+)|)?([\\s]*))*$";

            //start from beginning (^) and go to very end ($)
            //first optionally remove spaces [\s]* (this might not be necessary)
            //find optionally values without parameter. one of following:
            //  1) anything enclosed by double quotes ("[^"]*")
            //  2) anything enclosed by single quotes ('[^']*')
            //  3) anything that is not double or single quote nor slash nor minus [^"'/-]*
            //  4) or anything that contains no space [^ ]*?
            //find each parameter-value pair which seems to be okay. however, there might be unwanted some / or - signs in between.
            //each pair must start with a space followed by / or - ([\s]+[/-])
            //next is the parameter name which can be anything but spaces, -, /, or : ([^\\s-/:=])
            //next is the value which can either be one of following: (note: order matters)
            //  -anything except " enclosed by " or anything except ' enclosed by ' ((\"[^\"]*\")|('[^']*'))
            //  -anything but spaces not starting with / nor -  optionally enclosed by spaces (([\\s]*[^/-][^\\s]+[\\s]*))
            //  -anything but / or - ([^/-]+).
            //the argument may end with spaces (([\\s]*))

            RegexOptions ro = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            Regex parseCmdLine = new Regex(correctCmdLineRegEx, ro);

            //For test and debug purposes function Matches() is used which returns
            //a MatchCollection. However, there should never be more than one entry.
            /*MatchCollection mc = ParseCmdLine.Matches(ArgumentLine.ToString());
            if (mc.Count > 1)
                throw new Exception("Internal Exception: MatchCollection contains more than 1 entry");
            foreach (Match m in mc)*/

            //By default use Match() because in case of no match raising ExceptionSyntaxError would be skipped by Matches() and foreach.
            Match m = parseCmdLine.Match(argumentLine.ToString());
            {
                if (m.Success == false)
                {
                    //Regular expression did not match ArgumentLine. There might be two / or -.
                    //Find out up to where ArgumentLine seems to be okay and raise an exception reporting the rest.
                    int lastCorrectPosition = FindMismatchReasonInRegex(correctCmdLineRegEx, argumentLine);
                    string probableErrorCause = argumentLine.Substring(lastCorrectPosition);
                    throw new ExceptionSyntaxError(String("Exception") + String("ExceptionSyntaxError") + argumentLine +
                                                  String("ExceptionSyntaxError2") + probableErrorCause + String("ExceptionSyntaxError3"));
                }
                else
                {
                    //RegEx match ArgumentLine, thus syntax is ok.

                    //try to add values without parameters to FoundParameter using function
                    //AddNewFoundParameter(). Before adding move quotes if any.
                    //If those arguments are not allowed AddNewFoundParameter() raises an exception.
                    Group u_grp = m.Groups["UnknownValues"];
                    string? unknownValues;
                    if (u_grp != null && string.IsNullOrEmpty(u_grp.Value) && u_grp.Captures != null && u_grp.Captures.Count > 0)
                    {
                        string g = string.Empty;
                        foreach (Capture f in u_grp.Captures)
                        {
                            if (f != null)
                            {
                                g += f + " ";
                            }
                        }

                        unknownValues = g.TrimEnd();
                    }
                    else
                    {
                        unknownValues = u_grp?.Value;
                    }

                    if (unknownValues != null && !string.IsNullOrEmpty(unknownValues))
                    {
                        string unknown = unknownValues.Trim();
                        Regex enclosed = new Regex("^(\".*\")|('.*')$");
                        Match e = enclosed.Match(unknown);
                        if (e.Length != 0)
                        {
                            unknown = unknown.Substring(1, unknown.Length - 2);
                        }

                        //check whether this first (unknown) value is actually a boolean parameter. (e.g. /help)
                        //if it is a boolean parameter (or switch) add if as such.
                        bool unknownParameterHandled = false;
                        if (mWantedParameters.ContainsKey(unknown))
                        {
                            if (mWantedParameters[unknown].ValueType == ValueType.Bool || mWantedParameters[unknown].ValueType == ValueType.MultipleBool)
                            {
                                AddNewFoundParameter(unknown, string.Empty);
                                unknownParameterHandled = true;
                            }
                        }
                        else if ((unknown[0] == '/' || unknown[0] == '-') && mWantedParameters.ContainsKey(unknown.Substring(1)))
                        {
                            if (mWantedParameters[unknown.Substring(1)].ValueType == ValueType.Bool || mWantedParameters[unknown.Substring(1)].ValueType == ValueType.MultipleBool)
                            {
                                AddNewFoundParameter(unknown.Substring(1), string.Empty);
                                unknownParameterHandled = true;
                            }
                        }

                        //check if found (unknown) parameter is actually parameter-value-pair (e.g. /parameter="value"
                        //but only add it as parameter-value-pair if it was NOT quoted (e.Length == 0)
                        if (unknownParameterHandled == false && e.Length == 0)
                        {
                            parseCmdLine = new Regex(paramValuePairRegEx, ro);
                            Match pair = parseCmdLine.Match(unknown);
                            if (pair.Success == true)
                            {
                                Group param_grp3 = pair.Groups["name"];
                                Group value_grp3 = pair.Groups["value"];
                                if (param_grp3.Captures.Count != 1 || value_grp3.Captures.Count != 1)
                                {
                                    throw new Exception("Internal Exception: First parameter is parameter-value-pair but does not consist of exactly 1 parameter and 1 value. This should never happen.");
                                }

                                AddNewFoundParameter(param_grp3.Captures[0].ToString(), value_grp3.Captures[0].ToString());
                                unknownParameterHandled = true;
                            }
                        }

                        //is first (unknown) parameter was not processed yet (i.e. it is not boolean parameter nor parameter-value-pair),
                        //add it as unknown parameter now.
                        if (unknownParameterHandled == false)
                        {
                            AddNewFoundParameter(string.Empty, unknown);
                        }
                    }

                    Group prefix_grp = m.Groups["prefix"];
                    Group param_grp = m.Groups["name"];
                    Group value_grp = m.Groups["value"];
                    if (prefix_grp == null || param_grp == null || value_grp == null)
                    {
                        //this should never happen.
                        throw new Exception("Internal Exception: command-line parameter(s) incorrect.");
                    }

                    //RegEx find always pairs of name- and value-group. their count should thus always match.
                    if (param_grp.Captures.Count != value_grp.Captures.Count)
                    {
                        throw new Exception("Internal Exception: Number of parameters and number of values is not equal. This should never happen.");
                    }

                    //try to add each name-value-match to FoundParameters using AddNewFoundParameter() function.
                    //if value is quoted, remove quotes before calling AddNewFoundParameter().
                    //if value is of wrong type AddNewFoundParameter() throws an exception.
                    for (int i = 0; i < param_grp.Captures.Count; i++)
                    {
                        //if there are spaces at either side of value or parameter, trim those.
                        string value = value_grp.Captures[i].ToString().Trim();
                        string param = param_grp.Captures[i].ToString().Trim();
                        Regex enclosed = new Regex("^(\".*\")|('.*')$");
                        Match e = enclosed.Match(value);
                        if (e.Length != 0)
                        {
                            value = value.Substring(1, value.Length - 2);
                        }

                        // If current parameter is int without value and next parameter is negative number (e.g. -100),
                        // actually next parameter is the value for current parameter
                        if (string.IsNullOrEmpty(value)
                            && prefix_grp.Captures.Count == value_grp.Captures.Count
                            && mWantedParameters.ContainsKey(param) == true
                            && (mWantedParameters[param].ValueType == ValueType.Int
                                || mWantedParameters[param].ValueType == ValueType.OptionalInt)
                            && param_grp.Captures.Count > i + 1)
                        {
                            string nextValue = value_grp.Captures[i + 1].ToString().Trim();
                            string nextParam = param_grp.Captures[i + 1].ToString().Trim();
                            string nextPrefix = prefix_grp.Captures[i + 1].ToString().Trim();
                            if (string.IsNullOrEmpty(nextValue) && nextPrefix == "-" && int.TryParse(nextParam, out _))
                            {
                                value = "-" + nextParam;
                                i++;
                            }
                        }
                        else if (prefix_grp.Captures.Count == value_grp.Captures.Count
                          && mWantedParameters.ContainsKey(param) == true
                          && mWantedParameters[param].ValueType == ValueType.MultipleInts
                          && param_grp.Captures.Count > i + 1)
                        {
                            string nextValue = value_grp.Captures[i + 1].ToString().Trim();
                            string nextParam = param_grp.Captures[i + 1].ToString().Trim();
                            string nextPrefix = prefix_grp.Captures[i + 1].ToString().Trim();
                            if (nextPrefix == "-" && int.TryParse(nextParam, out _))
                            {
                                value += " -" + nextParam;
                                if (int.TryParse(nextValue, out _))
                                {
                                    value += " " + nextValue;
                                }

                                i++;
                            }
                        }

                        AddNewFoundParameter(param, value);
                    }
                }
            }

            CheckRequiredParameters();
        }

        /// <summary>
        /// <para>Obsolete. Please use Parse(void) instead.</para>
        /// <para>Starts the parsing process using arguments. Note that arguments is preprocessed by .NET (e.g. quotes are removed).</para>
        /// <para>Using this function user has to escape quotes of quoted arguments (e.g. my.exe /p \"argument with spaces\")</para>
        /// <para>Afterwards use the enumerator or the dictionary interface to access the found parameters and their values.</para>
        /// </summary>
        /// <param name="arguments">Arguments as string array passed via command line to the program.</param>
        public void Parse(string[] arguments)
        {
            //NOTE: IF PARSING DOES NOT WORK AS EXPECTED, TRY TO ESCAPE QUOTES (i.e. \" )
            //(from cmd.exe this seems to be necessary; instead single quotes could be used.)

            string mArgs = string.Empty;
            foreach (string s in arguments)
            {
                mArgs += s + " ";
            }

            Parse(mArgs);
        }

        /// <summary>
        /// <para>Starts the parsing process. Throws CmdLineArgumentExceptions in case of errors.</para>
        /// <para>Afterwards use the enumerator or the dictionary interface to access the found parameters and their values.</para>
        /// </summary>
        public void Parse()
        {
            Parse(GetRawCommandLineArgs());
        }

        private void CheckRequiredParameters()
        {
            foreach (KeyValuePair<string, ParameterDefintion> param in mWantedParameters)
            {
                if (param.Value.AllowType == ParamAllowType.Required)
                {
                    if (mFoundParameters.ContainsKey(param.Key) == false)
                    {
                        if (string.IsNullOrEmpty(param.Key))
                        {
                            throw new ExceptionRequiredParameterMissing(String("Exception") + String("ExceptionRequiredFirstParameterMissing"));
                        }
                        else
                        {
                            throw new ExceptionRequiredParameterMissing(String("Exception") + String("ExceptionRequiredParameterMissing") + param.Key + String("ExceptionRequiredParameterMissing2"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// <para>Creates information for command-line usage for user.</para>
        /// <para>To create this usage string information passed to CLAParser by function Parameter() is used.</para>
        /// <para>Format of returned string:</para>
        /// <para>&#182;</para>
        /// <para>Usage:</para>
        /// <para>name_of_program.exe /output_file &lt;string&gt; /character &lt;string&gt; /number &lt;int&gt; [/v [/v [...]]]</para>
        /// </summary>
        /// <returns>usage text</returns>
        public string GetUsage()
        {
            string usage = String("Usage") + "\r\n" + System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            string optionalBracketLeft = string.Empty;
            string optionalBracketRight = string.Empty;
            string paramString;

            for (int i = 0; i < 2; i++)
            {
                foreach (KeyValuePair<string, ParameterDefintion> param in mWantedParameters)
                {
                    //first take only required parameters then only optional
                    if (i == 0 && param.Value.AllowType == ParamAllowType.Optional && !string.IsNullOrEmpty(param.Key))
                    {
                        continue;
                    }
                    else if (i == 1 && (param.Value.AllowType == ParamAllowType.Required || (string.IsNullOrEmpty(param.Key) && param.Value.AllowType == ParamAllowType.Optional)))
                    {
                        continue;
                    }

                    paramString = param.Key;
                    if (param.Value.AllowType == ParamAllowType.Optional)
                    {
                        optionalBracketLeft = "[";
                        optionalBracketRight = "]";
                    }

                    //if this is the first parameter (ParamString == ""), we do not want this extra space.
                    string extraSpace = string.Empty;
                    string shownParameterPrefix = string.Empty;
                    if (!string.IsNullOrEmpty(paramString))
                    {
                        extraSpace = " ";
                        shownParameterPrefix = ParameterPrefix;
                    }

                    string value;
                    switch (param.Value.ValueType)
                    {
                        default:
                        case ValueType.Bool: extraSpace = string.Empty; value = string.Empty; break;
                        case ValueType.String: value = /*no leading space!*/ "<" + String("String") + ">"; break;
                        case ValueType.Int: value = " <" + String("Int") + ">"; break;
                        case ValueType.OptionalString: value = " [<" + String("String") + ">]"; break;
                        case ValueType.OptionalInt: value = " [<" + String("Int") + ">]"; break;
                        case ValueType.MultipleBool: value = string.Empty; paramString += " [" + ParameterPrefix + paramString + " [...]]"; break;
                        case ValueType.MultipleInts: value = " [<" + String("Int") + "1> [<" + String("Int") + "2> [...]]]"; break;
                    }

                    usage += " " + optionalBracketLeft + shownParameterPrefix + paramString + extraSpace + value + optionalBracketRight + " ";
                }
            }

            return usage;
        }

        /// <summary>
        /// <para>Creates information about each parameter which can be displayed to user as a help.</para>
        /// <para>To create this help string information passed to CLAParser by function Parameter() is used.</para>
        /// <para>Format of returned string:</para>
        /// <para>&#182;</para>
        /// <para>Parameters:</para>
        /// <para>Required:</para>
        /// <para>/output_file : Specify output file.</para>
        /// <para>/character   : Character to be written to output file.</para>
        /// <para>/number      : Number of times to write character to output file.</para>
        /// <para>&#182;</para>
        /// <para>Optional</para>
        /// <para>/v : Define (multiple) /v flag(s) for verbose output. Each /v increases verbosity more.</para>
        /// </summary>
        /// <returns>string with information about each parameter</returns>
        public string GetParameterInfo()
        {
            string parameterInfo = String("Parameters") + "\r\n";

            for (int i = 0; i < 2; i++)
            {
                //find the longest parameter for this section (i==0->required, i==1->optional)
                int longestparameter = 0;
                foreach (KeyValuePair<string, ParameterDefintion> param in mWantedParameters)
                {
                    var currentParam = param.Key;
                    //for the first parameter we need a different representation.
                    if (string.IsNullOrEmpty(currentParam))
                    {
                        currentParam = /*"<" +*/ String("FirstParam") + ">";
                    }

                    if ((i == 0 && param.Value.AllowType == ParamAllowType.Required) ||
                        (i == 1 && param.Value.AllowType == ParamAllowType.Optional))
                    {
                        if (longestparameter < currentParam.Length)
                        {
                            longestparameter = currentParam.Length;
                        }
                    }
                }

                //Print section header only of there is at least one parameter.
                if (longestparameter > 0 && i == 0)
                {
                    parameterInfo += String("Required") + "\r\n";
                }
                else if (longestparameter > 0 && i == 1)
                {
                    parameterInfo += "\r\n" + String("Optional") + "\r\n";
                }

                foreach (KeyValuePair<string, ParameterDefintion> param in mWantedParameters)
                {
                    var currentParam = param.Key;
                    var shownParameterPrefix = ParameterPrefix;
                    int makeUpForMissingSlash = 0;
                    //first take only required parameters then only optional
                    if (i == 0 && param.Value.AllowType == ParamAllowType.Optional)
                    {
                        continue;
                    }
                    else if (i == 1 && param.Value.AllowType == ParamAllowType.Required)
                    {
                        continue;
                    }

                    //for the first parameter we need a different representation.
                    if (string.IsNullOrEmpty(currentParam))
                    {
                        currentParam = "<" + String("FirstParam") + ">";
                        shownParameterPrefix = string.Empty;
                        makeUpForMissingSlash = 1;
                    }

                    parameterInfo += shownParameterPrefix + currentParam + new string(' ', makeUpForMissingSlash + longestparameter - currentParam.Length) + " : " + param.Value.Help + "\r\n";
                }
            }

            return parameterInfo;
        }

        /* Needed since Implementing IEnumerable*/

        /// <summary>
        /// Returns a enumerator which walks through the dictionary of found parameters.
        /// </summary>
        /// <returns>enumerator of dictionary of found parameters</returns>
        public IEnumerator GetEnumerator()
        {
            mEnumerator = mFoundParameters.GetEnumerator();
            return mEnumerator;
        }

        /* Needed since Implementing IEnumerator*/

        /// <summary>
        /// Sets the enumerator to the next found parameter.
        /// </summary>
        /// <returns>true if there is a next found parameter, else false</returns>
        public bool MoveNext()
        {
            return mEnumerator.MoveNext();
        }

        /// <summary>
        /// Resets the enumerator to the initial position in front of the first found parameter.
        /// </summary>
        public void Reset()
        {
            mEnumerator.Reset();
        }

        /// <summary>
        /// Returns the current found parameter from enumerator.
        /// </summary>
        public object Current => (DictionaryEntry?)mEnumerator.Current ?? throw new NullReferenceException();

        /// <summary>
        /// Returns the number of found parameters.
        /// </summary>
        public int Count => mFoundParameters.Count;

        /// <summary>
        /// <para>Obtains strings from the CmdLineArgResourceManager.</para>
        /// <para>If resource does not exist or ResourceManager has no data assigned,
        /// the supplied ResourceManagerString is returned.</para>
        /// </summary>
        /// <param name="resourceManagerString">Is looked up in ResourceManager</param>
        /// <returns>Match in ResourceManager or if no match available ResourceManagerString</returns>
        private string? String(string resourceManagerString)
        {
            string? ret;
            try
            {
                ret = mCmdLineArgResourceManager.GetString(resourceManagerString);
            }
            catch (MissingManifestResourceException)
            {
                throw new Exception("Internal Exception: MissingManifestResourceException: Make sure NamespaceOfResX passed constructor CLAParser() is correct. If the RESX-file is directly included to project, NamespaceOfResX must be the default namespace. In your main file Program.cs look for the line starting with \"namespace\" at top of file and try to pass the string which follows to constructor CLAParser().");
            }
            catch (Exception)
            {
                ret = resourceManagerString;
            }

            if (ret == null)
            {
                ret = resourceManagerString;
            }

            return ret;
        }

        /// <summary>
        /// <para>Call FindMismatchReasonInRegex() if SearchStr does not match RegEx in order to find out
        /// up to where SearchStr matches and where the mismatch starts.</para>
        /// <para>&#182;</para>
        /// <para>Decomposes regular expression RegEx into subexpressions according to parenthesis groupings.
        /// Each subexpression which can be matched, indicates that SearchStr is valid up to that position.
        /// Thus this function can find out up to which position SearchStr is valid and where probably
        /// an error is located.</para>
        /// </summary>
        /// <param name="regEx">Regular expression which is decomposed.</param>
        /// <param name="searchStr">String which does not match RegEx.</param>
        /// <returns>Returns the character position where the reason for the RegEx mismatch probably is located.</returns>
        private static int FindMismatchReasonInRegex(string regEx, string searchStr)
        {
            //disassemble RegEx string by finding all opening parentheses and their matching closing parts.
            SortedDictionary<int, int> parenthesis = new SortedDictionary<int, int>();
            Stack<int> openP = new Stack<int>();
            try
            {
                for (int i = 0; i < regEx.Length; i++)
                {
                    if (regEx[i] == '(')
                    {
                        //make sure that this ( is not escaped
                        if (!((i == 1 && regEx[i - 1] == '\\') ||
                               (i > 1 && regEx[i - 1] == '\\' && regEx[i - 2] != '\\')))
                        {
                            openP.Push(i);
                        }
                    }
                    else if (regEx[i] == ')')
                    {
                        //make sure that this ) is not escaped
                        if (!((i == 1 && regEx[i - 1] == '\\') ||
                               (i > 1 && regEx[i - 1] == '\\' && regEx[i - 2] != '\\')))
                        {
                            int pop = openP.Pop();
                            parenthesis.Add(pop, i);
                        }
                    }
                }

                //since RegEx should be valid, this can never happen.
                if (openP.Count != 0)
                {
                    throw new Exception("Internal Exception: Parenthesis not balanced");
                }
            }
            catch (Exception)
            {
                //since RegEx should be valid, this can never happen.
                throw new Exception("Internal Exception: Parenthesis not balanced");
            }

            //parenthesis contains all parenthesis matches ordered by the position of the opening parenthesis
            IEnumerator e = parenthesis.GetEnumerator();
            int lastCorrectPosition = 0;
            while (e.MoveNext())
            {
                if (e.Current == null) { break; }
                KeyValuePair<int, int> c = (KeyValuePair<int, int>)e.Current;

                //get sub-regular-expression of parenthesis grouping.
                string subRegEx = regEx.Substring(c.Key, c.Value - c.Key + 1);
                Regex sub;
                try
                {
                    sub = new Regex(subRegEx);
                }
                catch (Exception)
                {
                    //this should never happen since subexpression of a valid RegEx should still be valid.
                    throw new Exception("Internal Exception: SubRegEx invalid: " + subRegEx.ToString());
                }

                Match m = sub.Match(searchStr);
                if (m.Success == true)
                {
                    //if there is a match this subexpression matches the SearchStr and the mismatch must
                    //follow afterwards.
                    //find the end position of the match and increase LastCorrectPosition count to that position.
                    //(warning: here the wrong match might be detected,
                    //but since its is unlikely that command-line argument contains several identical parts,
                    //this potential problem is ignored.)
                    int newLastCorrectPosition = searchStr.IndexOf(m.Value, StringComparison.InvariantCultureIgnoreCase) + m.Value.Length;
                    if (newLastCorrectPosition > lastCorrectPosition)
                    {
                        lastCorrectPosition = newLastCorrectPosition;
                    }
                }
            }

            return lastCorrectPosition;
        }

        /// <summary>
        /// Adds and parameter-value-pair to FoundParameters if that pair matches the specification defined in WantedParameters.
        /// In case of a mismatch an exception is raised.
        /// </summary>
        /// <param name="newParam">The new parameter which is to be added to FoundParameters.</param>
        /// <param name="newValue">Value which corresponds to NewParam.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void AddNewFoundParameter(string newParam, string newValue)
        {
            //just to make sure, however this should never happen.
            if (newParam == null || newValue == null)
            {
                throw new Exception("Internal Exception: NewParam or NewValue in AddNewFoundParameter() == null");
            }

            //values without parameter is only allowed if WantedParameters contains "" or if additional parameters are allowed.
            if (string.IsNullOrEmpty(newParam) && mWantedParameters.ContainsKey(newParam) == false && AllowAdditionalParameters == false)
            {
                throw new ExceptionValueWithoutParameterFound(String("Exception") + String("ExceptionValueWithoutParameterFound") + newValue + String("ExceptionValueWithoutParameterFound2"));
            }
            else
            {
                //NewParam is not empty. Test if it is a wanted parameter, raise exception if not, else add it to FoundParameters.
                if (mWantedParameters.ContainsKey(newParam) == false && AllowAdditionalParameters == false)
                {
                    throw new ExceptionUnknownParameterFound(String("Exception") + String("ExceptionUnknownParameterFound") + newParam + String("ExceptionUnknownParameterFound2"));
                }
                else if (mWantedParameters.ContainsKey(newParam) == false && AllowAdditionalParameters == true)
                {
                    try
                    {
                        mFoundParameters.Add(newParam, newValue);
                    }
                    catch (ArgumentException)
                    {
                        throw new ExceptionRepeatedParameterFound(String("Exception") + String("ExceptionRepeatedParameterFound") + newParam + String("ExceptionRepeatedParameterFoundOnce"));
                    }
                }
                else if (mWantedParameters.ContainsKey(newParam) == true)
                {
                    //found parameter is wanted. check if value has right format for each ValueType.
                    switch (mWantedParameters[newParam].ValueType)
                    {
                        //bool parameters do not accept any value.
                        case ValueType.MultipleBool:
                        case ValueType.Bool:
                            if (!string.IsNullOrEmpty(newValue))
                            {
                                throw new ExceptionInvalidValueFound(String("Exception") + String("ExceptionInvalidValueFound") + newParam + String("ExceptionInvalidValueFoundBool"));
                            }

                            break;
                        //optionalInt might be empty, then make it 0 and treat like a normal int.
                        case ValueType.OptionalInt: if (string.IsNullOrEmpty(newValue)) { newValue = "0"; } goto case ValueType.Int; //"" is okay for OptionalInt
                        //int must be able to be converted to int32 without causing exception
                        case ValueType.Int: //else check if integer
                            try
                            {
                                Convert.ToInt32(newValue, System.Globalization.CultureInfo.InvariantCulture);
                            }
                            catch (Exception)
                            {
                                throw new ExceptionInvalidValueFound(String("Exception") + String("ExceptionInvalidValueFound") + newParam + String("ExceptionInvalidValueFoundInt"));
                            }

                            break;
                        //multipleInt must be split and then be converted to int32.
                        case ValueType.MultipleInts:
                            try
                            {
                                Regex split = new Regex("[\\s]+");
                                string[] values = split.Split(newValue);
                                foreach (string value in values)
                                {
                                    Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                                }
                            }
                            catch (Exception)
                            {
                                throw new ExceptionInvalidValueFound(String("Exception") + String("ExceptionInvalidValueFound") + newParam + String("ExceptionInvalidValueFoundInts"));
                            }

                            break;
                        //String can be anything but not empty.
                        case ValueType.String:
                            if (string.IsNullOrEmpty(newValue))
                            {
                                throw new ExceptionInvalidValueFound(String("Exception") + String("ExceptionInvalidValueFound") + newParam + String("ExceptionInvalidValueFoundString"));
                            }

                            break;
                        //OptionalString can be anything. No check necessary.
                        case ValueType.OptionalString: break;
                        //this should never happen because all cases are matched
                        default: throw new NotImplementedException($"case {mWantedParameters[newParam].ValueType}");
                    }

                    //now parameter is wanted and format is okay. insert parameter and value into FoundParameters
                    //if parameter does not already exists
                    //only exception: multipleBool
                    if (mFoundParameters.ContainsKey(newParam))
                    {
                        if (mWantedParameters[newParam].ValueType != ValueType.MultipleBool)
                        {
                            throw new ExceptionRepeatedParameterFound(String("Exception") + String("ExceptionRepeatedParameterFound") + newParam + String("ExceptionRepeatedParameterFoundOnce"));
                        }
                        else
                        {
                            mFoundParameters[newParam] = (Convert.ToInt32(mFoundParameters[newParam], System.Globalization.CultureInfo.InvariantCulture) + 1)
                                .ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                    else
                    {
                        if (mWantedParameters[newParam].ValueType == ValueType.MultipleBool)
                        {
                            mFoundParameters[newParam] = "1";
                        }
                        else
                        {
                            mFoundParameters.Add(newParam, newValue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// All parsing and argument errors raise exceptions which are inherited from CmdLineArgumentException.
        /// Other exceptions should not be raised if they are they indicate an internal problem of CLAParser.
        /// </summary>
        public class CmdLineArgumentException : Exception
        {
            public CmdLineArgumentException() { }

            public CmdLineArgumentException(string message) : base(message) { }

            public CmdLineArgumentException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if argument line contains a syntax error, e.g. when containing "/-"
        /// </summary>
        public class ExceptionSyntaxError : CmdLineArgumentException
        {
            public ExceptionSyntaxError() { }
            public ExceptionSyntaxError(string message) : base(message) { }

            public ExceptionSyntaxError(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if a parameter which was previously defined by Parameter() is not part of argument line.
        /// </summary>
        public class ExceptionRequiredParameterMissing : CmdLineArgumentException
        {
            public ExceptionRequiredParameterMissing() { }
            public ExceptionRequiredParameterMissing(string message) : base(message) { }

            public ExceptionRequiredParameterMissing(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if a parameter which was not previously defined by Parameter() is part of argument line and
        /// AllowAdditionalParameters is not set to true.
        /// </summary>
        public class ExceptionUnknownParameterFound : CmdLineArgumentException
        {
            public ExceptionUnknownParameterFound() { }
            public ExceptionUnknownParameterFound(string message) : base(message) { }

            public ExceptionUnknownParameterFound(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if a parameter holds a value which does not comply with its specification defined by
        /// previous call of Parameter().
        /// </summary>
        public class ExceptionInvalidValueFound : CmdLineArgumentException
        {
            public ExceptionInvalidValueFound() { }

            public ExceptionInvalidValueFound(string message) : base(message) { }

            public ExceptionInvalidValueFound(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if a parameter was used more than once, e.g. program.exe /p value1 /p value2
        /// Only exception for parameters of type MultipleBool; no exception raised for program.exe /mp /mp
        /// </summary>
        public class ExceptionRepeatedParameterFound : CmdLineArgumentException
        {
            public ExceptionRepeatedParameterFound() { }
            public ExceptionRepeatedParameterFound(string message) : base(message) { }

            public ExceptionRepeatedParameterFound(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>
        /// Raised if a parameter of type String, Int, or MultipleInts does not hold a value.
        /// </summary>
        public class ExceptionValueWithoutParameterFound : CmdLineArgumentException
        {
            public ExceptionValueWithoutParameterFound() { }
            public ExceptionValueWithoutParameterFound(string message) : base(message) { }

            public ExceptionValueWithoutParameterFound(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        /// <summary>This will return the raw, unprocessed command line parameters as a string.</summary>
        /// <returns>raw, unprocessed command line parameters as a string</returns>
        /// <remarks>Essentially, we are taking the unprocessed command-line and removing the executable path prefix by comparison to the .NET processed command-lineArg[0].
        /// Credits to vermis0 from www.codeproject.com</remarks>
        static public string GetRawCommandLineArgs()
        {
            string raw = Environment.CommandLine;
            string executablePath = Environment.GetCommandLineArgs()[0];
            string parsed;
            // Raw is the completely unprocessed command-line the OS used to launch our application. This includes the
            // path used to launch the executable as well as the parameters.
            // Our ExecutablePath is the .NET parsed path to the executable used to launch this application.
            // It can be a relative or absolute path. Since it was parsed by .NET, if the OS passed the executable
            // path as an encapsulated string, the surrounding " have been removed.
            //
            // If the raw command-line starts with a ", then we need to take that into account when we chop off
            // the executable path prefix (it was parsed out by .NET in ExecutablePath).
            if (raw.StartsWith("\"", StringComparison.InvariantCultureIgnoreCase)) { parsed = raw.Substring(executablePath.Length + 2); } else { parsed = raw.Substring(executablePath.Length); }
            // do not trim (needed for RegEx to work)
            return parsed;
        }
    }
}
