using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AssGen
{
	[Generator]
	public class AssGen : IIncrementalGenerator
	{
		public static string basePath;

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			IncrementalValuesProvider<AdditionalText> images = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".png") || file.Path.EndsWith("AssGenConfig.txt"));

			context.RegisterSourceOutput(images.Collect(), (spc, path) =>
			{
				var configPath = path.FirstOrDefault(n => n.Path.EndsWith("AssGenConfig.txt"))?.Path ?? null;

				if (configPath is null)
					throw new Exception("No configuration file provided! Please place a file named AssGenConfig.txt somewhere in your mod folder.");

				string assetRoot = null;

				using (FileStream configStream = File.OpenRead(configPath))
				{
					StreamReader reader = new StreamReader(configStream);

					while (true)
					{
						var line = reader.ReadLine();

						if (line is null)
							break;

						if (line.StartsWith("AssetRoot:"))
							assetRoot = line.Replace("AssetRoot:", "");
					}				
				}

				if (assetRoot is null)
					throw new Exception("No asset root provided! Please add the asset root to AssGenConfig.txt, in the form: AssetRoot:YourModName/YourAssetFolder");

				basePath = path.First(n => n.Path.EndsWith(".png"))?.Path.ToString().Split(new string[] { assetRoot }, StringSplitOptions.None)[0] ?? null;

				if (basePath is null)
					throw new Exception("No assets were found in your asset root! Make sure atleast one .png file exists somewhere in your assets folder.");

				string assetBasePath = Path.Combine(basePath, assetRoot);
				StringBuilder sb = new StringBuilder();
				GenerateClass(sb, assetBasePath);
				spc.AddSource("Assets.cs", "using ReLogic.Content;\nglobal using AssGen;\nnamespace AssGen\n{" + sb.ToString() + "}");
			});
		}

		public static void GenerateClass(StringBuilder sb, string dir)
		{
			string className = Path.GetFileName(dir);

			// Dont crawl hidden directories
			if (className.StartsWith("."))
				return;

			className = className.Replace(" ", "_").Replace(".", "_");

			// Prepend an underscore if it starts with a number
			if (char.IsDigit(className[0]))
				className = "_" + className;

			sb.AppendLine($"public class {className} {{");

			foreach (string file in Directory.EnumerateFiles(dir))
			{
				if (file.EndsWith(".png"))
				{
					string name = Path.GetFileNameWithoutExtension(file).Replace(" ", "_").Replace(".", "_");

					if (char.IsDigit(name[0]))
						name = "_" + name;

					if (name == className)
					{
						name += "_";
					}

					string codePath = file.Replace(basePath, "").Replace(".png", "").Replace("\\", "/");

					sb.AppendLine($"public static Asset<Texture2D> {name} = ModContent.Request<Texture2D>(\"{codePath}\");");
				}
			}

			foreach (string dir2 in Directory.EnumerateDirectories(dir))
			{
				GenerateClass(sb, dir2);
			}

			sb.AppendLine($"}}");
		}
	}
}
