/*

 Copyright (c) 2004-2006 Tomas Matousek.
  
 The use and distribution terms for this software are contained in the file named License.txt, 
 which can be found in the root of the Phalanger distribution. By using this software 
 in any fashion, you are agreeing to be bound by the terms of this license.
 
 You must not remove this notice from this software.

*/
using System;
using System.IO;
using System.Web;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using System.Web.SessionState;

namespace PHP.Core
{
	/// <summary>
	/// Generates request handlers servicing web requests.
	/// </summary>
	/// <threadsafety instance="true"/>
	public sealed class PageFactory : IHttpHandlerFactory
	{
		//internal static DateTime CurrentTimestamp;
		//internal static DateTime LastTimestamp;

		/// <summary>
		/// Initializes app domain settings.
		/// </summary>
		static PageFactory()
		{
			//Timestamp();

			// hook app-domain unload:
			// TODO (change code names of the assemblies to include the timestamp first):
			AppDomain.CurrentDomain.DomainUnload += new EventHandler(CurrentDomain_DomainUnload);

			// hook assembly load for debugging purposes:
			AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(AssemblyLoad);

			// turns on shadow copying on compiled pages directory:
            //AppDomain.CurrentDomain.SetShadowCopyPath(String.Concat(HttpRuntime.CodegenDir, ";", HttpRuntime.BinDirectory));
            //AppDomain.CurrentDomain.SetShadowCopyFiles();
            AppDomain.CurrentDomain.SetupInformation.ShadowCopyDirectories = String.Concat(HttpRuntime.CodegenDir, ";", HttpRuntime.BinDirectory);
            AppDomain.CurrentDomain.SetupInformation.ShadowCopyFiles = "true";

			Performance.Initialize();

			// creates a default context if not defined yet:
			ApplicationContext.DefineDefaultContext(false, false, true);
		}

		static void CurrentDomain_DomainUnload(object sender, EventArgs e)
		{
		//  try
		//  {
		//    // write a notice to the code-gen dir that the dynamic assemblies generated by this assembly are no longer used:
		//    File.WriteAllText(Path.Combine(HttpRuntime.CodegenDir, CurrentTimestamp.ToString() + ".delete"), "");
		//  }
		//  catch
		//  {
		//    // nop
		//  }	
		}

		//private static void Timestamp()
		//{
		//  const string TimestampFile = "Hash.web";

		//  string timestamp_file = Path.Combine(HttpRuntime.CodegenDir, TimestampFile);

		//  CurrentTimestamp = DateTime.Now;
		//  try
		//  {
		//    if (File.Exists(timestamp_file))
		//      LastTimestamp = new DateTime(Int64.Parse(File.ReadAllText(timestamp_file)));
		//    else
		//      LastTimestamp = DateTime.MinValue;	
		//  }
		//  catch
		//  {
		//    LastTimestamp = DateTime.MinValue;
		//  }

		//  try
		//  {
		//    File.WriteAllText(timestamp_file, CurrentTimestamp.Ticks.ToString());
		//  }
		//  catch
		//  {
		//    // nop
		//  }
		//}

		/// <summary>
		/// The method is called by ASP.NET server to obtain <see cref="IHttpHandler"/> implementor.
		/// </summary>
		/// <returns>Returns a <see cref="RequestHandler"/>.</returns>
		public IHttpHandler GetHandler(HttpContext context, string requestType, string url, string pathTranslated)
		{
			return new RequestHandler();
		}

		/// <summary>
		/// Enables a factory to reuse an existing handler instance.
		/// </summary>
		/// <param name="handler">The <see cref="IHttpHandler"/> object to reuse.</param>
		public void ReleaseHandler(IHttpHandler handler)
		{
		}

		/// <summary>
		/// Called on assembly load.
		/// </summary>
		private static void AssemblyLoad(object sender, AssemblyLoadEventArgs args)
		{
			Debug.WriteLine("APPDOMAIN", "Assembly load: '{0}'", args.LoadedAssembly.FullName);
		}
	}

	/// <summary>
	/// Process a request and stores references to objects associated with it.
	/// </summary>
	[Serializable]
	internal sealed class RequestHandler : IHttpHandler, IRequiresSessionState
	{
		/// <summary>
		/// Invoked by ASP.NET when a request comes from a client. Single threaded.
		/// </summary>
		/// <param name="context">The reference to web server objects.</param>
		/// <exception cref="System.Configuration.ConfigurationErrorsException">The configuration is invalid.</exception>
		/// <exception cref="PhpUserException">Uncaught exception.</exception>
		/// <exception cref="PhpNetInternalException">An internal error.</exception>
		/// <exception cref="Exception">Uncaught exception thrown by the class library or another error occurred during request processing.</exception>
		public void ProcessRequest(HttpContext/*!*/ context)
		{
			if (context == null)
				throw new ArgumentNullException("context");
            
            // disables ASP.NET timeout if possible:
			try { context.Server.ScriptTimeout = Int32.MaxValue; } catch (HttpException) { }

            // ensure that Session ID is created
			RequestContext.EnsureSessionId();
			
			// default culture:
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            
            using (RequestContext request_context = RequestContext.Initialize(ApplicationContext.Default, context))
            {
                Debug.WriteLine("REQUEST", "Processing request");
                if (request_context.ScriptContext.Config.Session.AutoStart)
                    request_context.StartSession();

                ScriptInfo script = null;
                try
                {
                    script = request_context.GetCompiledScript(request_context.RequestFile);

                    if (script != null)
                        request_context.IncludeScript(context.Request.PhysicalPath, script);
                }
                catch (PhpException)
                {
                    // A user code or compiler have reported a fatal error.
                    // We don't want to propagate the exception to web server.
                }
                catch (InvalidSourceException)
                {
                    // the source file could not be found neither in a script library and file system
                    context.Response.StatusCode = 404;
                }
            }
		}

		/// <summary>
		/// Whether another request can reuse this instance.
		/// All fields are reinitialized at the beginning of the request thus the instance is reusable.
		/// </summary>
		public bool IsReusable { get { return true; } }
	}
}
