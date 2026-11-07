using System.Text.Json;
using System.Text.Json.Serialization;
using MSMask.Core.Models;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// A labeled data set: the contents of a <c>labels.json</c> file.
    /// </summary>
    /// <remarks>
    /// The file has this shape (extra fields are ignored):
    /// <code>
    /// {
    ///   "seed": 1,
    ///   "images": [
    ///     { "file": "images/0000.png", "width": 900, "height": 600,
    ///       "labels": [ { "kind": "TCKN", "box": [40, 10, 150, 30], "text": "10000000146" } ] }
    ///   ]
    /// }
    /// </code>
    /// Boxes are <c>[x0, y0, x1, y1]</c> in pixels.
    /// </remarks>
    public sealed class LabelSet
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSet"/> class.
        /// </summary>
        /// <param name="path">The path of the <c>labels.json</c> file.</param>
        /// <param name="images">The labeled images.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public LabelSet(string path, IReadOnlyList<LabeledImage> images)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(images);
            Path = path;
            Images = images;
        }

        /// <summary>
        /// Gets the path of the <c>labels.json</c> file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the folder that the image paths are relative to.
        /// </summary>
        /// <value>The folder that contains <see cref="Path"/>.</value>
        public string Root => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) ?? ".";

        /// <summary>
        /// Gets the labeled images.
        /// </summary>
        public IReadOnlyList<LabeledImage> Images { get; }

        /// <summary>
        /// Gets the full path of an image file.
        /// </summary>
        /// <param name="image">An image of this set.</param>
        /// <returns>The absolute path of the image file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
        public string ImagePath(LabeledImage image)
        {
            ArgumentNullException.ThrowIfNull(image);
            return System.IO.Path.Combine(Root, image.File);
        }

        /// <summary>
        /// Reads a <c>labels.json</c> file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The labeled data set.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is not a valid label file.</exception>
        public static LabelSet Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The label file was not found.", path);
            }

            LabelFileDto? file;
            try
            {
                file = JsonSerializer.Deserialize<LabelFileDto>(File.ReadAllText(path));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"The label file '{path}' is not valid JSON: {ex.Message}", ex);
            }
            if (file?.Images is null)
            {
                throw new InvalidDataException($"The label file '{path}' has no \"images\" array.");
            }

            var images = new List<LabeledImage>(file.Images.Count);
            foreach (ImageDto image in file.Images)
            {
                var labels = new List<GroundTruthLabel>();
                foreach (LabelDto label in image.Labels ?? [])
                {
                    if (label.Box is not { Length: 4 } b)
                    {
                        throw new InvalidDataException($"A label of '{image.File}' does not have a box of four numbers.");
                    }
                    labels.Add(new GroundTruthLabel(label.Kind ?? "", new Box(b[0], b[1], b[2], b[3]), label.Text ?? ""));
                }
                images.Add(new LabeledImage(image.File ?? "", image.Width, image.Height, labels));
            }
            return new LabelSet(path, images);
        }

        /// <summary>
        /// The JSON shape of the whole file.
        /// </summary>
        private sealed class LabelFileDto
        {
            /// <summary>
            /// Gets or sets the images.
            /// </summary>
            [JsonPropertyName("images")]
            public List<ImageDto>? Images { get; set; }
        }

        /// <summary>
        /// The JSON shape of one image.
        /// </summary>
        private sealed class ImageDto
        {
            /// <summary>
            /// Gets or sets the image path relative to the label file.
            /// </summary>
            [JsonPropertyName("file")]
            public string? File { get; set; }

            /// <summary>
            /// Gets or sets the image width.
            /// </summary>
            [JsonPropertyName("width")]
            public int Width { get; set; }

            /// <summary>
            /// Gets or sets the image height.
            /// </summary>
            [JsonPropertyName("height")]
            public int Height { get; set; }

            /// <summary>
            /// Gets or sets the labels.
            /// </summary>
            [JsonPropertyName("labels")]
            public List<LabelDto>? Labels { get; set; }
        }

        /// <summary>
        /// The JSON shape of one label.
        /// </summary>
        private sealed class LabelDto
        {
            /// <summary>
            /// Gets or sets the kind.
            /// </summary>
            [JsonPropertyName("kind")]
            public string? Kind { get; set; }

            /// <summary>
            /// Gets or sets the box as <c>[x0, y0, x1, y1]</c>.
            /// </summary>
            [JsonPropertyName("box")]
            public double[]? Box { get; set; }

            /// <summary>
            /// Gets or sets the text.
            /// </summary>
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}