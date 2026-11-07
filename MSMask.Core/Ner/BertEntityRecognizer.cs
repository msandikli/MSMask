using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MSMask.Core.Ner
{
    /// <summary>
    /// Finds named entities with a BERT token classification model in ONNX format, running fully on this computer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model folder must contain three files: <c>model.onnx</c> (the network), <c>vocab.txt</c> (the piece
    /// vocabulary, see <see cref="WordPieceTokenizer"/>) and <c>config.json</c> (whose <c>id2label</c> section names
    /// the model's output classes, for example <c>O</c>, <c>B-PER</c>, <c>I-PER</c>).
    /// </para>
    /// <para>
    /// For every text the pieces are wrapped as <c>[CLS] pieces [SEP]</c>. Texts are processed in batches; shorter
    /// texts are filled with <c>[PAD]</c>, and the attention mask tells the model to ignore the filling. The model
    /// returns a score (logit) per class for every piece; softmax turns them into probabilities, the most likely
    /// class is kept, and <see cref="EntityAggregator"/> joins the pieces into entities.
    /// </para>
    /// <para>
    /// No text leaves the computer: inference runs in-process with ONNX Runtime.
    /// </para>
    /// </remarks>
    public sealed class BertEntityRecognizer : IEntityRecognizer, IDisposable
    {
        /// <summary>
        /// The environment variable that can point to the model folder.
        /// </summary>
        public const string ModelDirectoryEnvironmentVariable = "MSMASK_NER_MODEL_DIR";

        /// <summary>
        /// The name of the model folder inside a <c>models</c> folder.
        /// </summary>
        public const string DefaultModelFolder = "bert-base-turkish-cased-ner";

        /// <summary>
        /// The longest input the model accepts, in pieces, including <c>[CLS]</c> and <c>[SEP]</c>.
        /// </summary>
        public const int MaxSequenceLength = 512;

        /// <summary>
        /// The number of texts processed in one model call.
        /// </summary>
        public const int BatchSize = 32;

        /// <summary>
        /// The ONNX Runtime session that runs the model.
        /// </summary>
        private readonly InferenceSession _session;

        /// <summary>
        /// The tokenizer that matches the model's vocabulary.
        /// </summary>
        private readonly WordPieceTokenizer _tokenizer;

        /// <summary>
        /// The class names, indexed by the model's output position.
        /// </summary>
        private readonly string[] _labels;

        /// <summary>
        /// Whether the model expects a <c>token_type_ids</c> input.
        /// </summary>
        private readonly bool _needsTokenTypes;

        /// <summary>
        /// Whether <see cref="Dispose"/> has been called.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BertEntityRecognizer"/> class.
        /// </summary>
        /// <param name="modelDirectory">
        /// The folder with <c>model.onnx</c>, <c>vocab.txt</c> and <c>config.json</c>, or <see langword="null"/> to
        /// search for it with <see cref="FindModelDirectory"/>.
        /// </param>
        /// <exception cref="DirectoryNotFoundException">Thrown when no folder is given and none can be found.</exception>
        /// <exception cref="FileNotFoundException">Thrown when one of the three files is missing.</exception>
        /// <exception cref="InvalidDataException">Thrown when <c>config.json</c> has no valid label list.</exception>
        public BertEntityRecognizer(string? modelDirectory = null)
        {
            string directory = modelDirectory ?? FindModelDirectory()
                ?? throw new DirectoryNotFoundException(
                    $"No NER model was found. Put model.onnx, vocab.txt and config.json into 'models/{DefaultModelFolder}' " +
                    $"next to the application or above it, or set the {ModelDirectoryEnvironmentVariable} environment variable.");
            string modelPath = Path.Combine(directory, "model.onnx");
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("The model folder does not contain model.onnx.", modelPath);
            }

            ModelDirectory = directory;
            _tokenizer = WordPieceTokenizer.FromVocabularyFile(Path.Combine(directory, "vocab.txt"));
            _labels = ReadLabels(Path.Combine(directory, "config.json"));

            using var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
            _session = new InferenceSession(modelPath, options);
            _needsTokenTypes = _session.InputMetadata.ContainsKey("token_type_ids");
        }

        /// <summary>
        /// Gets the folder the model was loaded from.
        /// </summary>
        public string ModelDirectory { get; }

        /// <summary>
        /// Gets the class names of the model, in output order.
        /// </summary>
        /// <value>For example <c>["O", "B-PER", "I-PER", "B-ORG", "I-ORG", "B-LOC", "I-LOC"]</c>.</value>
        public IReadOnlyList<string> Labels => _labels;

        /// <inheritdoc/>
        public Task<IReadOnlyList<IReadOnlyList<NerEntity>>> RecognizeAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(texts);
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Inference is CPU-heavy, so it runs on a thread-pool thread.
            return Task.Run(() => Recognize(texts, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Releases the model.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _session.Dispose();
        }

        /// <summary>
        /// Searches for the model folder.
        /// </summary>
        /// <remarks>
        /// The places are checked in this order:
        /// <list type="number">
        /// <item><description>The folder named by the <see cref="ModelDirectoryEnvironmentVariable"/> environment variable.</description></item>
        /// <item><description>A <c>models/bert-base-turkish-cased-ner</c> folder in the application folder or any folder above it.</description></item>
        /// </list>
        /// </remarks>
        /// <returns>The first folder that contains <c>model.onnx</c>, or <see langword="null"/> if there is none.</returns>
        public static string? FindModelDirectory()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable(ModelDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(Path.Combine(fromEnvironment, "model.onnx")))
            {
                return fromEnvironment;
            }

            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "models", DefaultModelFolder);
                if (File.Exists(Path.Combine(candidate, "model.onnx")))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Converts raw model scores (logits) into probabilities that sum to 1.
        /// </summary>
        /// <remarks>The largest value is subtracted first so that <see cref="Math.Exp"/> cannot overflow.</remarks>
        /// <param name="logits">The scores of one piece, one per class.</param>
        /// <returns>The probabilities, in the same order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logits"/> is <see langword="null"/>.</exception>
        public static double[] Softmax(IReadOnlyList<float> logits)
        {
            ArgumentNullException.ThrowIfNull(logits);
            double max = logits.Count > 0 ? logits.Max() : 0.0;
            double[] result = logits.Select(v => Math.Exp(v - max)).ToArray();
            double sum = result.Sum();
            for (int i = 0; i < result.Length; i++)
            {
                result[i] /= sum;
            }
            return result;
        }

        /// <summary>
        /// Reads the class names from the <c>id2label</c> section of <c>config.json</c>.
        /// </summary>
        /// <param name="path">The path of <c>config.json</c>.</param>
        /// <returns>The class names, indexed by class id.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidDataException">Thrown when the label list is missing or has gaps.</exception>
        private static string[] ReadLabels(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The model folder does not contain config.json.", path);
            }
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("id2label", out JsonElement map) || map.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"'{path}' has no id2label section.");
            }

            var labels = new Dictionary<int, string>();
            foreach (JsonProperty entry in map.EnumerateObject())
            {
                if (!int.TryParse(entry.Name, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int id))
                {
                    throw new InvalidDataException($"'{path}' has a label with the invalid id '{entry.Name}'.");
                }
                labels[id] = entry.Value.GetString() ?? "";
            }
            string[] result = new string[labels.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = labels.TryGetValue(i, out string? label)
                    ? label
                    : throw new InvalidDataException($"'{path}' has no label with id {i}.");
            }
            return result;
        }

        /// <summary>
        /// Finds the entities in all texts, batch by batch.
        /// </summary>
        /// <param name="texts">The texts.</param>
        /// <param name="cancellationToken">A token that cancels the work between batches.</param>
        /// <returns>One entity list per text.</returns>
        private IReadOnlyList<IReadOnlyList<NerEntity>> Recognize(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            var results = new IReadOnlyList<NerEntity>[texts.Count];
            for (int first = 0; first < texts.Count; first += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(BatchSize, texts.Count - first);
                var batch = new List<List<WordPiece>>(count);
                for (int i = 0; i < count; i++)
                {
                    List<WordPiece> pieces = _tokenizer.Tokenize(texts[first + i]);
                    if (pieces.Count > MaxSequenceLength - 2)
                    {
                        // Room is kept for [CLS] and [SEP]; the rest of a very long text is not read.
                        pieces.RemoveRange(MaxSequenceLength - 2, pieces.Count - (MaxSequenceLength - 2));
                    }
                    batch.Add(pieces);
                }

                List<List<TokenPrediction>> predictions = Predict(batch);
                for (int i = 0; i < count; i++)
                {
                    results[first + i] = EntityAggregator.Aggregate(predictions[i]);
                }
            }
            return results;
        }

        /// <summary>
        /// Runs the model on one batch and keeps the most likely class of every piece.
        /// </summary>
        /// <param name="batch">The pieces of each text, without special pieces.</param>
        /// <returns>The prediction for every piece of every text.</returns>
        private List<List<TokenPrediction>> Predict(List<List<WordPiece>> batch)
        {
            int length = batch.Max(p => p.Count) + 2;
            int[] shape = [batch.Count, length];
            var ids = new DenseTensor<long>(shape);
            var mask = new DenseTensor<long>(shape);
            var types = new DenseTensor<long>(shape);

            for (int b = 0; b < batch.Count; b++)
            {
                List<WordPiece> pieces = batch[b];
                ids[b, 0] = _tokenizer.ClassId;
                for (int j = 0; j < pieces.Count; j++)
                {
                    ids[b, j + 1] = pieces[j].Id;
                }
                ids[b, pieces.Count + 1] = _tokenizer.SeparatorId;
                for (int j = 0; j < length; j++)
                {
                    if (j <= pieces.Count + 1)
                    {
                        mask[b, j] = 1;
                    }
                    else
                    {
                        ids[b, j] = _tokenizer.PaddingId;
                    }
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", ids),
                NamedOnnxValue.CreateFromTensor("attention_mask", mask),
            };
            if (_needsTokenTypes)
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", types));
            }

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);
            Tensor<float> logits = outputs[0].AsTensor<float>();
            int classCount = _labels.Length;

            var result = new List<List<TokenPrediction>>(batch.Count);
            var scores = new float[classCount];
            for (int b = 0; b < batch.Count; b++)
            {
                var predictions = new List<TokenPrediction>(batch[b].Count);
                for (int j = 0; j < batch[b].Count; j++)
                {
                    for (int c = 0; c < classCount; c++)
                    {
                        // Position j + 1 because position 0 holds [CLS].
                        scores[c] = logits[b, j + 1, c];
                    }
                    double[] probabilities = Softmax(scores);
                    int best = 0;
                    for (int c = 1; c < classCount; c++)
                    {
                        if (probabilities[c] > probabilities[best])
                        {
                            best = c;
                        }
                    }
                    WordPiece piece = batch[b][j];
                    bool isSubword = piece.Text.StartsWith(WordPieceTokenizer.ContinuationPrefix, StringComparison.Ordinal);
                    predictions.Add(new TokenPrediction(_labels[best], probabilities[best], piece.Start, piece.End, isSubword));
                }
                result.Add(predictions);
            }
            return result;
        }
    }
}