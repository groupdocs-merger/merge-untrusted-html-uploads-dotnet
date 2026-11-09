// Topic: Merge untrusted HTML uploads without letting them reach the network,
// read local files or pull in another customer's files.
// Uses GroupDocs.Merger for .NET 26.10: secure defaults, IExternalResourceHandler
// and WhitelistedResources.

using System.Collections.Concurrent;
using GroupDocs.Merger;
using GroupDocs.Merger.Domain;
using GroupDocs.Merger.Domain.Options;

namespace Demo.UntrustedMerge;

internal static class Program
{
    private const string InputFolder = "resources";
    private const string ResultFolder = "output";
    private const string LogoUrl = "https://cdn.example.com/brand/logo.png";

    private static readonly string InvoiceUpload =
        Path.Combine(InputFolder, "uploads", "invoice-upload.html");
    private static readonly string DeliveryNoteUpload =
        Path.Combine(InputFolder, "uploads", "delivery-note-upload.html");
    private static readonly string LogoFile =
        Path.Combine(InputFolder, "brand", "logo.png");

    private static int Main()
    {
        Directory.CreateDirectory(ResultFolder);
        ApplyLicense();

        foreach (string input in new[] { InvoiceUpload, DeliveryNoteUpload, LogoFile })
        {
            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Missing input: {Path.GetFullPath(input)}");
                return 1;
            }
        }

        Console.WriteLine("1. Two HTML uploads joined with the default options:");
        int defaults = JoinHtmlUploads(
            InvoiceUpload, DeliveryNoteUpload, ResultPath("joined.html"));
        Console.WriteLine($"   loaded: {defaults}");

        Console.WriteLine("2. Same uploads, company logo supplied from a local file:");
        int withLogo = JoinWithTrustedLogo(
            InvoiceUpload, DeliveryNoteUpload, LogoUrl, LogoFile,
            ResultPath("joined-with-logo.html"));
        Console.WriteLine($"   loaded or supplied: {withLogo}");

        Console.WriteLine("3. Whitelist entry that is only a host name:");
        string? rejection = RejectLooseWhitelistEntry(InvoiceUpload, "cdn.example.com");
        Console.WriteLine(rejection == null ? "   accepted" : "   rejected");

        Console.WriteLine($"Output folder: {Path.GetFullPath(ResultFolder)}");
        bool asExpected = defaults == 0 && withLogo == 1 && rejection != null;
        return asExpected ? 0 : 2;
    }

    private static string ResultPath(string fileName) =>
        Path.Combine(ResultFolder, fileName);

    private static void ApplyLicense()
    {
        // Point this at your .lic file to remove evaluation limits.
        // Get a free temporary licence: https://purchase.groupdocs.com/temporary-license
        const string licensePath = "REPLACE_WITH_YOUR_LICENSE_PATH";
        if (File.Exists(licensePath))
        {
            new License().SetLicense(licensePath);
            Console.WriteLine("[license] applied");
        }
        else
        {
            Console.WriteLine("[license] no licence set - running in evaluation mode");
        }
    }

    /// <summary>
    /// Joins two uploaded HTML files with the default options and lists every external
    /// resource they asked for.
    /// </summary>
    /// <remarks>
    /// The defaults deny remote URLs, UNC paths and local files, including a file saved
    /// in the upload's own folder. The joined document follows the options of the Merger
    /// it is joined to, so one LoadOptions covers both uploads. Returns the number of
    /// resources that loaded.
    /// </remarks>
    private static int JoinHtmlUploads(string firstUpload, string secondUpload,
        string outputPath)
    {
        var audit = new ResourceAudit();
        var loadOptions = new LoadOptions(FileType.HTML) { ExternalResourceHandler = audit };

        using (var merger = new Merger(firstUpload, loadOptions))
        {
            merger.Join(secondUpload);
            merger.Save(outputPath);
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Joins the uploads with the company logo they reference served from a local file.
    /// </summary>
    /// <remarks>
    /// The handler answers the logo's URL with the bytes of a file you control, so the
    /// joined document shows the logo and nothing is requested over the network. Every
    /// other resource keeps the default decision. Returns the number of resources that
    /// loaded or were supplied.
    /// </remarks>
    private static int JoinWithTrustedLogo(string firstUpload, string secondUpload,
        string logoUrl, string logoPath, string outputPath)
    {
        var trustedAssets = new Dictionary<string, byte[]>
        {
            [logoUrl] = File.ReadAllBytes(logoPath)
        };
        var audit = new ResourceAudit(trustedAssets);
        var loadOptions = new LoadOptions(FileType.HTML) { ExternalResourceHandler = audit };

        using (var merger = new Merger(firstUpload, loadOptions))
        {
            merger.Join(secondUpload);
            merger.Save(outputPath);
        }

        foreach (string decision in audit.Decisions)
        {
            Console.WriteLine($"   {decision}");
        }

        return audit.Loaded;
    }

    /// <summary>
    /// Shows that a whitelist entry which is not an absolute URI prefix is rejected.
    /// </summary>
    /// <remarks>
    /// Each WhitelistedResources entry must be the absolute prefix of a trusted location,
    /// such as https://cdn.example.com/brand/. A bare host name makes the Merger
    /// constructor throw ArgumentException, whose message names the entry, before any
    /// document is read. Returns that message, or null when the entry is accepted.
    /// </remarks>
    private static string? RejectLooseWhitelistEntry(string uploadPath, string entry)
    {
        var loadOptions = new LoadOptions(FileType.HTML)
        {
            WhitelistedResources = new List<string> { entry }
        };

        try
        {
            using var merger = new Merger(uploadPath, loadOptions);
            merger.Save(ResultPath("loose-whitelist.html"));
            return null;
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"   {exception.Message}");
            return exception.Message;
        }
    }
}

/// <summary>
/// Records each resource a document asks for and supplies trusted assets from local files.
/// </summary>
internal sealed class ResourceAudit : IExternalResourceHandler
{
    private readonly IReadOnlyDictionary<string, byte[]> _trustedAssets;
    private readonly ConcurrentQueue<string> _decisions = new();

    public ResourceAudit()
        : this(new Dictionary<string, byte[]>())
    {
    }

    public ResourceAudit(IReadOnlyDictionary<string, byte[]> trustedAssets)
    {
        _trustedAssets = trustedAssets;
    }

    public IEnumerable<string> Decisions => _decisions;

    public int Loaded => _decisions.Count(decision => !decision.StartsWith("blocked"));

    public void OnLoading(ExternalResourceLoadingArgs args)
    {
        if (_trustedAssets.TryGetValue(args.Uri, out byte[]? content))
        {
            args.Skipped = false;
            args.Data = content;
        }

        string decision = args.Skipped ? "blocked "
            : args.Data != null ? "supplied"
            : "loaded  ";
        _decisions.Enqueue($"{decision} {args.Uri}");
    }
}
