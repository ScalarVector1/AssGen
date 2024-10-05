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
				// Here we check for and attempt to grab the config file from the text context
				AdditionalText config = path.FirstOrDefault(n => n.Path.EndsWith("AssGenConfig.txt")) ?? null;
				string configPath = config?.Path ?? null;

				// We throw if there is no config
				if (config is null || configPath is null)
					throw new Exception("No configuration file provided! Please place a file named AssGenConfig.txt somewhere in your mod folder.");

				// Here we get the text content of the config file to set the asset root variable to know where to scan from
				string assetRoot = null;

				foreach (Microsoft.CodeAnalysis.Text.TextLine line in config.GetText().Lines)
				{
					if (line.Text.ToString().StartsWith("AssetRoot:"))
						assetRoot = line.Text.ToString().Replace("AssetRoot:", "");
				}

				// We throw if there is no asset root in the config
				if (assetRoot is null)
					throw new Exception("No asset root provided! Please add the asset root to AssGenConfig.txt, in the form: AssetRoot:YourModName/YourAssetFolder");

				// We get the first path in the provider, and split it along the asset root to get the base path on the file system.
				basePath = path.First(n => n.Path.EndsWith(".png"))?.Path.ToString().Split(new string[] { assetRoot }, StringSplitOptions.None)[0] ?? null;

				// We throw if there are no images in the project that start with the asset root
				if (basePath is null)
					throw new Exception("No assets were found in your asset root! Make sure atleast one .png file exists somewhere in your assets folder.");

				// Create the string builder for the source
				var sb = new StringBuilder();

				// Now we generate the partial chain for each image
				foreach (AdditionalText img in path)
				{
					if (img.Path == configPath) // ignore the config
						continue;

					// We split the path as everything after the root, and by the subdirectory seperator. TODO: Something to make sure this wont die with different path seperators?
					var split = img.Path.ToString().Split(new string[] { assetRoot }, StringSplitOptions.None)[1].Split('\\').ToList();
					var fileName = split[split.Count - 1].Replace(".png", ""); // Save the file name for later
					split.RemoveAt(split.Count - 1); // Remove the last element as that is the file name

					if (split.Count > 1) // if we're in a subdirectory
					{
						foreach (var sub in split) // generate a partial nested class for each directory. The compiler figures out that these are all the same thing later. Godspeed, C# compiler
						{
							if (!string.IsNullOrEmpty(sub))
							{
								var clean = CleanName(sub);
								sb.Append($"public partial class {clean}{{");
							}
						}
					}

					// sanitize the file name to get the name of the member
					string name = CleanName(fileName);

					// if equal to the containing class, add an underscore to force it to be unique
					if (split.Count() > 1 && name == split[split.Count() - 2])
					{
						name += "_";
					}

					// generate the path that this would need to be as a string for ModContent.Request
					string codePath = img.Path.Replace(basePath, "").Replace(".png", "").Replace("\\", "/");

					// append the member
					sb.Append($"public static Asset<Texture2D> {name} = ModContent.Request<Texture2D>(\"{codePath}\");");

					// add appropriate amount of closing braces
					if (split.Count > 1)
					{
						for(int k = 0; k < split.Count - 1; k++)
						{
							sb.Append("}");
						}
					}

					// add a linebreak for nice looking output
					sb.Append("\n");
				}

				// Add global usings, then the inner source
				spc.AddSource("Assets.cs", "global using AssGen;\nusing Microsoft.Xna.Framework.Graphics;\nusing ReLogic.Content;\nusing Terraria.ModLoader;\nnamespace AssGen\n{\npublic class Assets{" + sb.ToString() + "}}");
			});
		}

		/// <summary>
		/// Sanitize a name used as a class or member name
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static string CleanName(string input)
		{
			input = input.Replace(" ", "_").Replace(".", "_").Replace("-", "_");

			// Prepend an underscore if it starts with a number
			if (char.IsDigit(input[0]))
				input = "_" + input;

			// Ensure we're not using a keyword
			if (Keywords.keywords.Contains(input))
				input = "_" + input;

			return input;
		}
	}
}
