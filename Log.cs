using System;
using System.IO;

namespace Rdr3CrashDiagnostics
{
	public static class Log
	{
		public static readonly string LogFileName = $"CrashDiag-{DateTime.Now:yy-MM-dd}.log";

		public static void Info( string msg ) {
			WriteLine( "INFO", msg, ConsoleColor.White );
		}

		public static void Warn( string msg ) {
			WriteLine( "WARN", msg, ConsoleColor.Yellow );
		}

		public static void Error( string msg ) {
			WriteLine( "ERROR", msg, ConsoleColor.Red );
		}

		public static void Error( Exception ex, string msg = "" ) {
			WriteLine( "ERROR", $"{msg}\r\n{ex}", ConsoleColor.Red );
		}

		public static void Verbose( string msg ) {
#if DEBUG
			WriteLine( "VERBOSE", msg, ConsoleColor.DarkGray );
#endif
		}

		private static void WriteLine( string title, string msg, ConsoleColor color ) {
			try {
				var m = $"{DateTime.Now:G} [{title}] {msg}";
				Console.ForegroundColor = color;
				Console.WriteLine( m );
				Console.ResetColor();
				using( var writer = File.AppendText( LogFileName ) ) {
					writer.WriteLine( m );
					writer.Flush();
				}
			}
			catch( Exception ex ) {
				Console.WriteLine( ex );
			}
		}
	}
}
