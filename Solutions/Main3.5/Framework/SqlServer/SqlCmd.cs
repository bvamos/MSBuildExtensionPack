﻿//-----------------------------------------------------------------------
// <copyright file="SqlCmd.cs">(c) http://www.codeplex.com/MSBuildExtensionPack. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.SqlServer
{
    using System;
    using System.IO;    
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    /// Wraps the SQL Server command line executable SqlCmd.exe.
    /// <para />
    /// <b>Valid TaskActions are:</b>
    /// <para><i>Execute</i> (<b>Required: </b>)</para>
    /// </summary>
    /// <example>
    /// <code lang="xml"><![CDATA[
    /// <Project ToolsVersion="3.5" DefaultTargets="Default" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    ///     <PropertyGroup>
    ///         <TPath>$(MSBuildProjectDirectory)\..\MSBuild.ExtensionPack.tasks</TPath>
    ///         <TPath Condition="Exists('$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks')">$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks</TPath>
    ///     </PropertyGroup>
    ///     <Import Project="$(TPath)"/>
    ///     <ItemGroup>
    ///         <InputFile>C:\File1.sql</InputFile>
    ///         <InputFile>C:\File2.sql</InputFile>
    ///         <InputFile>C:\File3.sql</InputFile>
    ///     </ItemGroup>
    ///     <ItemGroup>
    ///         <Variable Name="MyVar">AdventureWorks</Variable>
    ///         <Variable Name="MyVar1">Master</Variable>
    ///     </ItemGroup>
    ///     <Target Name="Default">
    ///         <!-- Perfrom various sql server operations -->
    ///         <MSBuild.ExtensionPack.SqlServer.SqlCmd TaskAction="Execute" LoginId="sa" Password="sa" CommandLineQuery="SELECT @@VERSION;" />
    ///         <MSBuild.ExtensionPack.SqlServer.SqlCmd TaskAction="Execute" TrustedConnection="true" Server="(local)" Database="AdventureWorks" InputFiles="@(InputFile)"/>
    ///         <MSBuild.ExtensionPack.SqlServer.SqlCmd TaskAction="Execute" TrustedConnection="true" Server="(local)" Database="AdventureWorks" CommandLineQuery="SELECT @@VERSION;" OutputFile="C:\Output.txt" />
    ///         <MSBuild.ExtensionPack.SqlServer.SqlCmd TaskAction="Execute" TrustedConnection="true" Server="(local)" Database="AdventureWorks" InputFiles="@(InputFile)" OutputFile="C:\Output.txt" Variables="@(Variable)" />
    ///     </Target>
    /// </Project>
    /// ]]></code>    
    /// </example>  
    public class SqlCmd : BaseTask
    {
        private const string ExecutionMessage = "Executing '{0}' with '{1}'";
        private const string InputFileAndQuerySpecifiedError = "SqlCmd: The InputFiles and Query options are mutually exclusive.";
        private const string InputFileMessage = "Adding input file '{0}'";
        private const string InvalidSqlCmdPathError = "Unable to resolve path to sqlcmd.exe. Assuming it is in the PATH environment variable.";
        private const string InvalidTaskActionError = "Invalid TaskAction passed: {0}";
        private const string LoginTimeOutRangeError = "The LoginTimeOut value specified '{0}' does not fall in the allowed range of 0 to 65534. Using the default value of eight (8) seconds.";
        private const string InvalidLoginArgumentsSpecified = "The TrustedConnection and LoginId/Password options are mutually exclusive.";
        private const string QueryMessage = "Adding query '{0}'";
        private const string QueryTimeOutRangeError = "The QueryTimeOut value specified '{0}' does not fall in the allowed range of 1 to 65535.";

        private int loginTimeOut = 8;
        private int queryTimeOut = 0;
        private string server = ".";

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCmd"/> class.
        /// </summary>
        public SqlCmd()
        {
            this.DisableVariableSubstitution = false;
            this.EchoInput = false;
            this.RedirectStandardError = false;
            this.TrustedConnection = true;
            this.UseClientRegionalSettings = false;
        }

        /// <summary>
        /// Gets or sets the path to the sqlcmd.exe.
        /// </summary>
        public string SqlCmdPath { get; set; }

#region Login Related Options

        /// <summary>
        /// <para>Gets or sets the user login id. If neither the <see cref="LoginId"/> or <see cref="Password"/> option is specified,
        /// <see cref="SqlCmd"/> tries to connect by using Microsoft Windows Authentication mode. Authentication is
        /// based on the Windows account of the user who is running <see cref="SqlCmd"/>.</para>
        /// <para>If the <see cref="LoginId"/> option is used with the <see cref="TrustedConnection"/> option, an error message
        /// is generated.</para>
        /// <para><b>Note:</b> The <i>OSQLUSER</i> environment variable is available for backwards compatibility. The <i>
        /// SQLCMDUSER</i> environment variable takes precedence over the <i>OSQLUSER</i> environment variable. This 
        /// means that <see cref="SqlCmd"/> and <b>osql</b> can be used next to each other without interference.</para>
        /// </summary>
        public string LoginId { get; set; }

        /// <summary>
        /// <para>Gets or sets the user specified password. Passwords are case-sensitive. If the <see cref="LoginId"/> option
        /// is used and the <see cref="Password"/> option is not used, and the <i>SQLCMDPASSWORD</i> environment variable
        /// has not been set, <see cref="SqlCmd"/> uses the default password (NULL).</para>
        /// <para><b>Note:</b> If the <see cref="Password"/> option is used with the <see cref="TrustedConnection"/> option
        /// an error message is generated.</para>
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// <para>Gets or sets a flag indicating the use of a trusted connection instead of using a username and password to
        /// log on to SQL Server. By default, without the <see cref="TrustedConnection"/> flag set, the <see cref="SqlCmd"/>
        /// uses the trusted connection option.</para>
        /// <para>The <see cref="TrustedConnection"/> option ignores possible user name and password environmental 
        /// variable settings such as <i>SQLCMDPASSWORD</i>. If the <see cref="TrustedConnection"/> option is used together
        /// with the <see cref="LoginId"/> option or the <see cref="Password"/> option, an error message is generated.</para>
        /// </summary>
        public bool TrustedConnection { get; set; }

        /// <summary>
        /// Changes the password for a user.
        /// </summary>
        public string NewPassword { get; set; }

        /// <summary>
        /// <para>Gets or sets the name of the SQL Server to which to connect. It sets the <see cref="SqlCmd"/> scripting variable 
        /// <i>SQLCMDSERVER</i>.</para>
        /// <para>Specify <see cref="Server"/> to connect to the default instance of SQL Server on that server computer. Specify 
        /// <see cref="Server"/> to connect to a named instance of SQL Server on that server computer. If no server computer is 
        /// specified, <see cref="SqlCmd"/> connects to the default instance of SQL Server on the local computer. This option is 
        /// required when you execute sqlcmd from a remote computer on the network.</para>
        /// <para>If you do not specify a <see cref="Server" /> when you start <see cref="SqlCmd" />, SQL Server checks for and 
        /// uses the <i>SQLCMDSERVER</i> environment variable.</para>
        /// <para><b>Note: </b>The <i>OSQLSERVER</i> environment variable has been kept for backward compatibility. The 
        /// <i>SQLCMDSERVER</i> environment variable takes precedence over the <i>OSQLSERVER</i> environment variable.</para>
        /// </summary>
        public string Server
        {
            get { return this.server; }
            set { this.server = value; }
        }

        /// <summary>
        /// Gets or sets the workstation name. This option sets the <see cref="SqlCmd"/> scripting variable <i>SQLCMDWORKSTATION</i>. 
        /// The workstation name is listed in the <b>hostname</b> column of the <b>sys.processes</b> catalog view and can be returned 
        /// using the stored procedure <b>sp_who</b>. If this option is not specified, the default is the current computer name. This name 
        /// can be used to identify different sqlcmd sessions.
        /// </summary>
        public string WorkStationName { get; set; }

        /// <summary>
        /// Gets or sets the name of the database to connect to. Issues a <code>USE</code> <i>db_name</i> statement when you start 
        /// <see cref="SqlCmd"/>. This option sets the <see cref="SqlCmd"/> scripting variable <i>SQLCMDDBNAME</i>. This specifies 
        /// the initial database. The default is your login's default-database property. If the database does not exist, an error message 
        /// is generated and <see cref="SqlCmd"/> exits.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds before the <see cref="SqlCmd"/> login to the OLE DB provider times out when
        /// you try to connect to a server. The default login time-out for <see cref="SqlCmd"/> is eight (8) seconds. The login time-
        /// out value must be a number between 0 and 65534. If the value supplied is not numeric or does not fall into that range,
        /// the <see cref="SqlCmd"/> generates an error message. A value of 0 specifies the time-out to be indefinite.
        /// </summary>
        public int LoginTimeOut
        {
            get
            {
                return this.loginTimeOut;
            }

            set
            {
                if (value >= 0 && value <= 65534)
                {
                    this.loginTimeOut = value;
                }
                else
                {
                    // Through an error
                    this.Log.LogWarning(LoginTimeOutRangeError, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag that indicates if the connection to SQL Server should use a Dedicated Administrator Connection (DAC).
        /// This kind of connection is used to troubleshoot a server. This will only work with server computers that support DAC. If 
        /// DAC is not available, <see cref="SqlCmd"/> generates an error message and then exits. For more information about DAC, see 
        /// <a href="http://msdn.microsoft.com/en-us/library/ms189595.aspx">Using a Dedicated Administrator Connection</a>.
        /// </summary>
        public bool DedicatedAdminConnection { get; set; }

#endregion

#region Input/Output Options

        /// <summary>
        /// <para>Gets or sets the path to a file that contains a batch of SQL statements. Multiple files may be specified that will be read 
        /// and processed in order. Do not use any spaces between the file names. <see cref="SqlCmd"/> will first check to see 
        /// whether all files exist. If one or more files do not exist, <see cref="SqlCmd"/> will exit. The <see cref="InputFiles"/> and
        /// <see cref="CommandLineQuery"/> options are mutually exclusive.</para>        
        /// </summary>
        public ITaskItem[] InputFiles { get; set; }

        /// <summary>
        /// <para>Gets or sets the file that receives output from <see cref="SqlCmd"/>.</para>
        /// <para>If the <see cref="UnicodeOutput"/> option is specified, the <i>output file</i> is stored in Unicode format.
        /// If the file name is not valid, an error message is generated, and <see cref="SqlCmd"/> exits. <see cref="SqlCmd"/> does 
        /// not support concurrent writing of multiple <see cref="SqlCmd"/> processes to the same file. The file output will be 
        /// corrupted or incorrect. See the <!--see cref="CodePage"/--> CodePage switch for more information about file formats. This file 
        /// will be created if it does not exist. A file of the same name from a prior <see cref="SqlCmd"/> session will be 
        /// overwritten. The file specified here is not the stdout file. If a stdout file is specified this file will not be used.</para>
        /// </summary>
        public string OutputFile { get; set; }

        ////public string CodePage { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicates if the <see cref="OutputFile"/> is stored in Unicode format, regardless of the 
        /// format of the <see cref="InputFiles"/>.
        /// </summary>
        public bool UnicodeOutput { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicates whether or not to redirect the error message output to the screen 
        /// (<b>stderr</b>).If you do not specify a parameter or if you specify <b>0</b>, only error messages that 
        /// have a severity level of 11 or higher are redirected. If you specify <b>1</b>, all error message output including 
        /// PRINT is redirected. Has no effect if you use <see cref="OutputFile"/>. By default, messages are sent to <b>stdout</b>.
        /// </summary>
        public bool RedirectStandardError { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicates if the SQL Server OLE DB provider uses the client regional settings when it converts
        /// currency, and date and time data to character data. The default is server regional settings.
        /// </summary>
        public bool UseClientRegionalSettings { get; set; }

#endregion

#region Query Execution Options

        /// <summary>
        /// Gets or sets one or more command line queries to execute when <see cref="SqlCmd"/> starts, but does not exit
        /// sqlcmd when the query has finished running.
        /// </summary>
        public ITaskItem[] CommandLineQuery { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicates if the input scripts are written to the standard output device (<b>stdout</b>).
        /// </summary>
        public bool EchoInput { get; set; }

        /// <summary>
        /// Gets or sets a flag that sets the <code>SET QUOTED_IDENTIFIER</code> connection option to <code>ON</code>. By 
        /// default, it is set to <code>OFF</code>. For more information, see 
        /// <a href="http://msdn.microsoft.com/en-us/library/ms174393.aspx">SET QUOTED_IDENTIFIER (Transact-SQL).</a>
        /// </summary>
        public bool EnableQuotedIdentifiers { get; set; }

        /// <summary>
        /// <para>Gets or sets the number of seconds before a command (or SQL statement) times out. This option sets the <see cref="SqlCmd"/>
        /// scripting variable <i>SQLCMDSTATTIMEOUT</i>. If a <i>time_out</i> value is not specified, the command does not time out. The 
        /// query <i>time_out</i> must be a number between 1 and 65535. If the value supplied is not numeric or does not fall into that range,
        /// <see cref="SqlCmd"/> generates an error message.</para>
        /// <para><b>Note:</b> The actual time out value may vary from the specified <i>time_out</i> value by several seconds.</para>
        /// </summary>
        public int QueryTimeout
        {
            get
            {
                if (this.queryTimeOut < 1)
                {
                    return 1;
                }
                else
                {
                    return this.queryTimeOut;
                }
            }

            set
            {
                if (value >= 1 && value <= 65535)
                {
                    this.queryTimeOut = value;
                }
                else
                {
                    // Through an error
                    this.Log.LogWarning(QueryTimeOutRangeError, value);
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="SqlCmd"/> scripting variable that can be used in a <see cref="SqlCmd"/> script. You can specify multiple 
        /// <see cref="Variables"/> and values. If there are errors in any of the values specified, <see cref="SqlCmd"/> generates an error 
        /// message and then exits.
        /// </summary>
        public ITaskItem[] Variables { get; set; }

        /// <summary>
        /// Causes <see cref="SqlCmd"/> to ignore scripting variables. This is useful when a script contains many INSERT statements that 
        /// may contain strings that have the same format as regular variables, such as $(variable_name).
        /// </summary>
        public bool DisableVariableSubstitution { get; set; }

#endregion

#region Formatting Options

        /// <summary>
        /// Specifies the number of rows to print between the column headings. The default is to print headings one time for each set of 
        /// query results. This option sets the sqlcmd scripting variable <i>SQLCMDHEADERS</i>. Use -1 to specify that headers must not be 
        /// printed. Any value that is not valid causes <see cref="SqlCmd"/> to generate an error message and then exit.
        /// </summary>
        public int Headers { get; set; }

        /*
        public string ColumnSeparator { get; set; }
        public int ColumnWidth { get; set; }
        public bool RemoveTrailingSpaces { get; set; }
        public bool RemoveControlCharacters { get; set; }
        public int DisplayFixedTypeWidth { get; set; }
        public int DisplayVariableTypeWidth { get; set; }
         */
#endregion

#region Error Reporting Options

        /*
        public bool OnErrorBatchAbort { get; set; }
        public int SeverityLevel { get; set; }
        public int ErrorLevel { get; set; }
         */
#endregion

#region Miscellaneous Options

        /*
        public int PacketSize { get; set; }
        public string BatchTerminator { get; set; }
        public string ListServers { get; set; }
        public bool PrintStatistics { get; set; }
        public bool DisableCommands { get; set; }
        public bool ShowUsage { get; set; }
         */

#endregion

        protected override void InternalExecute()
        {
            switch (this.TaskAction.ToUpperInvariant())
            {
                case "EXECUTE":
                    this.SqlExecute();
                    break;
                default:
                    this.Log.LogError(InvalidTaskActionError, this.TaskAction);
                    return;
            }
        }

        private string BuildArguments()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // Login Related Options

            // Login Id
            if (!string.IsNullOrEmpty(this.LoginId))
            {
                sb.Append(" -U ");
                sb.Append(this.LoginId);
            }

            // Password
            if (!string.IsNullOrEmpty(this.Password))
            {
                sb.Append(" -P ");
                sb.Append(this.Password);
            }

            // Trusted Connection
            if (this.TrustedConnection)
            {
                sb.Append(" -E ");
            }

            // New Password and exit
            if (!string.IsNullOrEmpty(this.NewPassword))
            {
                sb.Append(" -Z ");
                sb.Append(this.NewPassword);
            }

            // Server
            if (!string.IsNullOrEmpty(this.Server))
            {
                sb.Append(" -S ");
                sb.Append(this.server);
            }

            // Workstation
            if (!string.IsNullOrEmpty(this.WorkStationName))
            {
                sb.Append(" -H ");
                sb.Append(this.WorkStationName);
            }

            if (!string.IsNullOrEmpty(this.Database))
            {
                sb.Append(" -d ");
                sb.Append(this.Database);
            }

            // Login Timeout
            sb.Append(" -l ");
            sb.Append(this.LoginTimeOut);

            if (this.DedicatedAdminConnection)
            {
                sb.Append(" -A ");
            }

            // Input/Output Options

            // Input Files
            if (this.InputFiles != null)
            {
                foreach (ITaskItem file in this.InputFiles)
                {
                    this.Log.LogMessage(MessageImportance.Low, InputFileMessage, file.ItemSpec);
                    sb.Append(" -i ");
                    sb.Append("\"");
                    sb.Append(file.ItemSpec);
                    sb.Append("\"");
                }
            }

            // Output file
            if (!string.IsNullOrEmpty(this.OutputFile))
            {
                sb.Append(" -o ");
                sb.Append("\"");
                sb.Append(this.OutputFile);
                sb.Append("\"");
            }

            // Code Page

            // Unicode
            if (this.UnicodeOutput)
            {
                sb.Append(" -u ");
            }

            // Redirect Standard Error
            if (this.RedirectStandardError)
            {
                sb.Append(" -r 1 ");
            }

            // Client Regional settings
            if (this.UseClientRegionalSettings)
            {
                sb.Append(" -R ");
            }

            // Query Execution Options

            // Command line query
            if (this.CommandLineQuery != null)
            {
                foreach (ITaskItem query in this.CommandLineQuery)
                {
                    this.Log.LogMessage(MessageImportance.Low, QueryMessage, query.ItemSpec);
                    sb.Append(" -Q ");
                    sb.Append("\"");
                    sb.Append(query.ItemSpec);
                    sb.Append("\"");
                }
            }

            // Echo Input
            if (this.EchoInput)
            {
                sb.Append(" - e ");
            }

            // Enabled Quoted Identifiers
            if (this.EnableQuotedIdentifiers)
            {
                sb.Append(" -I ");
            }

            // Query timeout
            if (this.QueryTimeout > 0)
            {
                sb.Append(" -t ");
                sb.Append(this.QueryTimeout);
            }

            // Variables
            if (this.Variables != null)
            {
                foreach (ITaskItem variable in this.Variables)
                {
                    sb.Append(" -v ");
                    sb.Append(variable.GetMetadata("Name"));
                    sb.Append("=\"");
                    sb.Append(variable.ItemSpec);
                    sb.Append("\"");
                }
            }

            // DisableVariableSubstitution
            if (this.DisableVariableSubstitution)
            {
                sb.Append(" -x ");
            }

            return sb.ToString();
        }

        private void ExecuteCommand(string arguments)
        {
            var sqlCmdWrapper = new SqlCmdWrapper(this.SqlCmdPath, arguments);

            this.Log.LogMessage(MessageImportance.Low, ExecutionMessage, sqlCmdWrapper.Executable, arguments);

            // Get the return value
            int returnValue = sqlCmdWrapper.Execute();

            // Write out the output
            if (!string.IsNullOrEmpty(sqlCmdWrapper.StandardOutput))
            {
                this.Log.LogMessage(MessageImportance.Normal, sqlCmdWrapper.StandardOutput);
            }

            // Write out any errors
            this.SwitchReturnValue(returnValue, sqlCmdWrapper.StandardError.Trim());
        }

        private void SqlExecute()
        {
            // Resolve the path to the sqlcmd.exe tool
            if (!System.IO.File.Exists(this.SqlCmdPath))
            {
                this.Log.LogMessage(MessageImportance.Low, InvalidSqlCmdPathError);
                this.SqlCmdPath = "sqlcmd.exe";
            }

            if (!this.ValidateArguments())
            {
                // Exit
                return;
            }

            // Build out the arguments
            var arguments = this.BuildArguments();

#if DEBUG
            Console.WriteLine(arguments);
#endif

            this.ExecuteCommand(arguments);
        }

        private void SwitchReturnValue(int returnValue, string error)
        {
            switch (returnValue)
            {
                case 1:
                    this.LogTaskWarning("Exit Code 1. Failure: " + error);
                    break;
            }
        }

        private bool ValidateArguments()
        {
            if ((!string.IsNullOrEmpty(this.LoginId) || !string.IsNullOrEmpty(this.Password))
                && this.TrustedConnection)
            {
                // Log the error and exit
                this.Log.LogError(InvalidLoginArgumentsSpecified);
                return false;
            }

            // Default return
            return true;
        }
    }
}