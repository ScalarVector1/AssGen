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
			IncrementalValuesProvider<AdditionalText> images = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".png"));

			context.RegisterSourceOutput(images.Collect(), (spc, path) =>
			{
				Console.WriteLine(path.ToString());
				basePath = path.First().Path.ToString().Split(new string[] { "StarlightRiver\\Assets" }, StringSplitOptions.None)[0];
				string assetBasePath = Path.Combine(basePath, "StarlightRiver\\Assets");
				StringBuilder sb = new StringBuilder();
				GenerateClass(sb, assetBasePath);
				spc.AddSource("Assets.cs", "namespace StarlightRiver.Core\n{" + sb.ToString() + "}");
			});
		}

		public static void GenerateClass(StringBuilder sb, string dir)
		{
			string className = Path.GetFileName(dir);
			sb.AppendLine($"public class {className} {{");

			foreach (string file in Directory.EnumerateFiles(dir))
			{
				if (file.EndsWith(".png"))
				{
					string name = Path.GetFileNameWithoutExtension(file).Replace(" ", "_").Replace(".", "_");

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
