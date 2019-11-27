using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

namespace Rdr3CrashDiagnostics
{
	internal static class Program
	{
		private static readonly string SystemXmlPath =
			Path.Combine( Environment.GetEnvironmentVariable( "USERPROFILE" ) ?? "", "Documents", "Rockstar Games",
				"Red Dead Redemption 2", "Settings", "system.xml" );

		private static readonly string DefaultGamePath =
			Path.Combine( Environment.GetEnvironmentVariable( "PROGRAMFILES" ) ?? "", "Rockstar Games",
				"Red Dead Redemption 2" );

		[STAThread]
		public static void Main( string[] args ) {
			try {
				var zipName = $"CrashDiagnostics-{DateTime.Now:yy-MM-dd}.zip";
				var path = Path.Combine( Directory.GetCurrentDirectory(), zipName );
				if( File.Exists( path ) ) {
					File.Delete( path );
				}
				using( var fileStream = new FileStream( path, FileMode.CreateNew ) ) {
					using( var archive = new ZipArchive( fileStream, ZipArchiveMode.Create, true ) ) {
						PackGameSettings( archive );
						PackDxDiag( archive );
						PackGameFiles( archive );
						Log.Info( $"Successfully packed diagnostic data into: {path}" );
						PackLogFile( archive );

						MessageBox.Show(
							$"The Crash Diagnostics Tool has finished running! Send the following file to the developers who requested you to run it:\r\n\r\n{zipName}",
							"Crash Diagnostics Tool", MessageBoxButtons.OK, MessageBoxIcon.Information );
					}
				}
			}
			catch( Exception ex ) {
				Log.Error( ex );
				MessageBox.Show( "Could not pack diagnostic data. Please send the developers the Diagnostics Log file.",
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
			Environment.Exit( 0 );
		}

		private static void PackGameSettings( ZipArchive zip ) {
			Log.Info( "Retrieving game settings file" );
			if( !File.Exists( SystemXmlPath ) ) {
				Log.Warn( "No game settings file could be found." );
			}
			else {
				var data = File.ReadAllBytes( SystemXmlPath );
				var entry = zip.CreateEntry( "system.xml", CompressionLevel.Optimal );
				using( var stream = entry.Open() ) {
					stream.Write( data, 0, data.Length );
				}
				Log.Info( "Packed game settings file" );
			}
		}

		private static void PackDxDiag( ZipArchive zip ) {
			try {
				Log.Info( "Retrieving DirectX Diagnostics" );
				var path = RunDxDiag();
				var data = File.ReadAllBytes( path );
				var entry = zip.CreateEntry( "dxdiag.xml", CompressionLevel.Optimal );
				using( var stream = entry.Open() ) {
					stream.Write( data, 0, data.Length );
				}
				Log.Info( "Packed DirectX Diagnostics" );
				File.Delete( path );
			}
			catch( Exception ex ) {
				Log.Error( ex, "Failed to retrieve DirectX Diagnostics" );
			}
		}

		[STAThread]
		private static void PackGameFiles( ZipArchive zip ) {
			while( true ) {
				var path = DefaultGamePath;
				if( !Directory.Exists( path ) || !File.Exists( Path.Combine( path, "RDR2.exe" ) ) ) {
					Log.Warn( "Could not find game at default folder. Prompting user for game directory path." );
					var dialog = new FolderBrowserDialog { Description = "Select your RDR2 Game Folder" };
					dialog.ShowDialog();
					path = dialog.SelectedPath;
				}

				var rdr3 = Path.Combine( path, "RDR2.exe" );
				if( !File.Exists( rdr3 ) ) {
					Log.Warn( "Could not find RDR2.exe in specified path." );
					continue;
				}
				Log.Info( $"Found Game Folder: {path}" );

				// Grab game version
				var version = FileVersionInfo.GetVersionInfo( rdr3 ).FileVersion;
				Log.Info( $"Game Version: {version}" );

				// Grab md5 checksum of all files
				Log.Info( "Grabbing hierarchy of game directory" );
				DumpFileHierarchy( zip, "fs_game_folder.txt", path );

				// Grab md5 checksum of all scripts/* files
				var scripts = Path.Combine( path, "scripts" );
				if( Directory.Exists( scripts ) ) {
					Log.Info( "Grabbing hierarchy of scripts directory" );
					DumpFileHierarchy( zip, "fx_scripts_folder.txt", scripts );
				}
				else {
					Log.Warn( "No scripts directory found, skipping." );
				}


				// Grab log files
				Log.Info( "Grabbing *.log files" );
				foreach( var file in Directory.GetFiles( path, "*.log" ) ) {
					var entry = zip.CreateEntry( file, CompressionLevel.Optimal );
					var text = File.ReadAllBytes( file );
					using( var stream = entry.Open() ) {
						stream.Write( text, 0, text.Length );
					}
				}

				Log.Info( "Finished packing game files" );
				break;
			}
		}

		private static void PackLogFile( ZipArchive zip ) {
			var entry = zip.CreateEntry( "CrashDiagnostics.log" );
			var bytes = File.ReadAllBytes( Log.LogFileName );
			using( var stream = entry.Open() ) {
				stream.Write( bytes, 0, bytes.Length );
			}
			File.Delete( Log.LogFileName );
		}

		private static void DumpFileHierarchy( ZipArchive zip, string fileName, string path ) {
			var list = new List<string>();
			foreach( var file in Directory.GetFileSystemEntries( path ) ) {
				list.Add( file );
			}
			var checksumEntry = zip.CreateEntry( fileName, CompressionLevel.Optimal );
			var checksumBytes = Encoding.UTF8.GetBytes( string.Join( "\r\n", list ) );
			using( var stream = checksumEntry.Open() ) {
				stream.Write( checksumBytes, 0, checksumBytes.Length );
			}
		}

		private static string RunDxDiag() {
			var psi = new ProcessStartInfo();
			if( IntPtr.Size == 4 && Environment.Is64BitOperatingSystem ) {
				psi.FileName = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Windows ),
					"sysnative\\dxdiag.exe" );
			}
			else {
				psi.FileName = Path.Combine( Environment.SystemDirectory, "dxdiag.exe" );
			}
			var path = Path.GetTempFileName();
			psi.Arguments = $"/x {path}";
			using( var prc = Process.Start( psi ) ) {
				prc?.WaitForExit();
				if( prc == null || prc.ExitCode != 0 ) {
					throw new Exception( prc == null ? "Failed to start process." : $"failed with exit code {prc.ExitCode}" );
				}
			}
			return path;
		}
	}
}
