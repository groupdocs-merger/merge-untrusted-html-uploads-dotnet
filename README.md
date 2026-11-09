# Merge Untrusted HTML Documents Safely in .NET

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://products.groupdocs.com/merger/net)
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/merger/net/)
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/categories/groupdocs.merger-product-family/)
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/merger/)
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temporary-license)

## Introduction

`merge-untrusted-html-uploads-dotnet` is a runnable .NET console project that joins two customer-submitted HTML uploads into one document with GroupDocs.Merger for .NET, while refusing the external resources those uploads reference. Whether you're building an invoicing portal that merges a customer's HTML upload with a delivery note, or any workflow that joins documents it did not author itself, this repository shows how the merge treats the images, stylesheets and local paths an upload asks for.

When we ran it, the reference to `other-customer-signature.jpg` came back `blocked` just like the cloud metadata URL, although that file sits right next to the invoice upload.

### Why can't I just trust a relative image path inside an HTML upload?

A relative reference such as `other-customer-signature.jpg` looks like it points at a file the merge already controls. The sample's `ResourceAudit` handler receives it alongside the cloud metadata URL and the tracking pixel, and under the default options it arrives with `Skipped` already set, so the file is never read.

## Use Case Scenarios

This repository addresses the following use cases:

- **Invoice and delivery-note consolidation**: a customer portal joins two HTML uploads from the same order into a single document for archiving.
- **Multi-tenant document pipelines**: a merge service processes HTML from many customers and must not let one upload reach another customer's files or the host's local filesystem.
- **Server-side HTML ingestion**: any backend that merges HTML it received over an upload form, where the HTML can contain `<img>` and `<link>` references to anything the uploader chose to write.
- **Brand-asset exceptions**: a service that merges customer HTML but still wants its own logo or stylesheet to render, from a source it controls rather than the network.

## The Problem

Up to version 26.9, GroupDocs.Merger fetched the remote and absolute local references in an HTML upload while joining it, such as a stylesheet on another server, a remote image or a `file:///` path. No load option switched that off. An HTML upload is attacker-controlled content: a `<link rel="stylesheet" href="https://tracker.example.net/...">` or an `<img src="file:///etc/passwd">` is just as valid HTML as a real invoice stylesheet, and the merge could not tell them apart.

This matters most in multi-tenant and server-to-server scenarios, where a merge job runs on infrastructure the uploader never sees. An absolute `file:///` reference to another customer's upload looks like any other image, and before 26.10 the merge read that file into the merged output.

### Common Challenges

- ❌ An HTML upload can request any `http://`, `https://`, `ftp://`, UNC or local path, and the merge had no way to refuse.
- ❌ An absolute `file:///` reference can point at a file on the server that was never meant to be read by this merge.
- ❌ Letting a merge job make outbound HTTP requests on an attacker's behalf opens the door to SSRF against internal services (for example a cloud metadata endpoint).
- ❌ A legitimate exception, such as the company's own logo, still needs a way to be granted without reopening every other request.

## The Solution

GroupDocs.Merger for .NET merges HTML documents from sources you do not fully trust without loading what they reference. Since version 26.10, every `LoadOptions` constructor sets `SkipExternalResources = true` by default, and joining without explicit load options uses that same default. Key capabilities include:

✅ **Deny-by-default loading** – `http`, `https`, `ftp` and every other scheme, UNC paths and local files (including files in the upload's own folder) are refused unless explicitly allowed.
✅ **`WhitelistedResources`** – absolute URI prefixes you trust (scheme, host, port and path boundary must match) are allowed through while everything else stays denied.
✅ **`IExternalResourceHandler`** – a per-resource callback that can supply trusted bytes for one reference without reopening the rest.
✅ **Shared options across a join** – documents joined to one `Merger` follow that `Merger`'s `LoadOptions`, so one set of rules covers every upload in the merge.
✅ **No change to packaged content** – media the document already carries internally (for formats that have it) is unaffected by these options.

## Implementation Workflow

Here's a typical workflow for implementing merge-untrusted-documents-safely:

1. **Create `LoadOptions` for the format**: `new LoadOptions(FileType.HTML)` - there is no parameterless constructor, and `LoadOptions` is sealed since 26.10.
2. **Decide whether you need an exception**: if every external reference should simply be dropped, the defaults alone are enough.
3. **Attach an `IExternalResourceHandler`** when you need to supply a specific trusted asset (for example your own logo) from bytes you control.
4. **Create the `Merger` with those options, then `Join` the second upload** - both uploads are now governed by the same `LoadOptions`.
5. **Save the result** and inspect what the handler recorded, if you attached one, to confirm which references were blocked, loaded or supplied.

## Requirements

To use these examples, you'll need:

- **.NET SDK**: net8.0 (the project targets `net8.0`; GroupDocs.Merger for .NET also supports net462, net6.0 and net10.0).
- **GroupDocs.Merger for .NET 26.10.0**: referenced via NuGet, [`GroupDocs.Merger`](https://www.nuget.org/packages/GroupDocs.Merger/).
- **A license (optional)**: the sample runs unlicensed in evaluation mode; get a [temporary license](https://purchase.groupdocs.com/temporary-license) to remove evaluation limits.

## Project Structure

```
merge-untrusted-html-uploads-dotnet/
│
├── Program.cs
├── UntrustedMergeDemo.csproj
└── resources/
    ├── brand/
    │   └── logo.png
    └── uploads/
        ├── delivery-note-upload.html
        ├── invoice-upload.html
        └── other-customer-signature.jpg
```

**File Organization:**
- **Program.cs** – the three documented methods below, plus the `ResourceAudit` handler class ([lines 172-207](https://github.com/groupdocs-merger/merge-untrusted-html-uploads-dotnet/blob/main/Program.cs#L172-L207)) they share.
- **UntrustedMergeDemo.csproj** – targets net8.0 and references `GroupDocs.Merger` 26.10.0.
- **resources/uploads/** – the two HTML uploads being joined, plus a same-folder image standing in for another customer's file.
- **resources/brand/logo.png** – the trusted logo file the handler supplies in place of the remote URL.

## Practical Examples

### Use Case: Joins two uploaded HTML files with the default options and lists every external resource they asked for

**When to Use:** Use this when you are merging HTML uploads and want the safe behavior with no extra configuration - the defaults already deny everything external.

The defaults deny remote URLs, UNC paths and local files, including a file saved in the upload's own folder. The joined document follows the options of the `Merger` it is joined to, so one `LoadOptions` covers both uploads. The method returns the number of resources that loaded.

```csharp
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
```

**What This Solves:** Every external reference in both uploads - a remote stylesheet, a remote image, a cloud metadata URL, the sibling-file reference, a `file:///` path and a UNC path - is denied without any handler logic telling it to be. When we ran this against the invoice and delivery-note uploads, all seven references came back `blocked`, including `other-customer-signature.jpg`, which sits in the very same folder as the upload.

**Real-World Application:** A portal that merges a customer's invoice HTML with a delivery note can do so without ever making an outbound request on the customer's behalf, closing a path to SSRF against internal infrastructure.

### Use Case: Joins the uploads with the company logo they reference served from a local file

**When to Use:** Use this when one specific external reference - your own brand asset - should render, while everything else keeps the default (denied) behavior.

The handler answers the logo's URL with the bytes of a file you control, so the joined document shows the logo and nothing is requested over the network. Every other resource keeps the default decision. The method returns the number of resources that loaded or were supplied.

```csharp
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
```

**What This Solves:** The handler's `OnLoading` callback checks the requested URI against a dictionary of bytes it already has on disk; when it matches, it sets `args.Skipped = false` and `args.Data` to those bytes, so the logo is embedded in `joined-with-logo.html` without any HTTP request. The six other references in the two uploads stay blocked.

**Real-World Application:** A service that joins customer HTML but wants its own header image or stylesheet to render can supply that one asset explicitly, instead of disabling the deny-by-default behavior for everything.

### Use Case: Shows that a whitelist entry which is not an absolute URI prefix is rejected

**When to Use:** Use this when you plan to configure `WhitelistedResources` and need to know what a valid entry looks like before the code runs.

Each `WhitelistedResources` entry must be the absolute prefix of a trusted location, such as `https://cdn.example.com/brand/`. A bare host name makes the `Merger` constructor throw `ArgumentException`, whose message names the entry, before any document is read. The method returns that message, or `null` when the entry is accepted.

```csharp
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
```

**What This Solves:** Passing `"cdn.example.com"` as a whitelist entry fails fast, with the message `Whitelisted resource 'cdn.example.com' must be an absolute URI without a query, fragment or encoded '/', '\' or '.'. (Parameter 'WhitelistedResources')`, rather than silently being ignored or silently matching more than intended.

**Real-World Application:** Catching this at construction time, before any upload is read, means a misconfigured whitelist entry fails your deployment's startup checks instead of surfacing as a confusing "resource still blocked" bug in production.

## Benefits

By using GroupDocs.Merger for .NET for merge-untrusted-documents-safely, you gain:

- **Safe-by-default joins**: you do not have to remember to configure anything to get the safer behavior - it is the default since 26.10.
- **Precise exceptions**: `IExternalResourceHandler` lets you allow exactly one asset, from bytes you supply, instead of an all-or-nothing switch.
- **Fail-fast configuration**: an invalid `WhitelistedResources` entry throws at `Merger` construction, not somewhere deep in the merge.
- **One policy per join**: `LoadOptions` is set once on the `Merger` and governs every document joined to it.
- **No same-folder exemption**: a relative reference to a file in the uploads folder is denied exactly like any other untrusted reference.

## Related Use Cases and Resources

If you're implementing safe merging of untrusted HTML uploads or working on similar scenarios, these resources will help you:

* **Step-by-step use case guide in the documentation** – a worked walkthrough of the same three scenarios, with the full run output: [Read the article →](https://docs.groupdocs.com/merger/net/getting-started/use-cases/merge-untrusted-documents-safely/)

* **In-depth blog article about this project** – the before/after story of merging untrusted HTML before and after GroupDocs.Merger 26.10: [Read the article →](https://blog.groupdocs.com/merger/merge-untrusted-documents-safely-net/)

* **Skip Loading External Resources** – the documentation page this sample's defaults are based on: [Read the article →](https://docs.groupdocs.com/merger/net/skip-loading-external-resources/)

* **Network Access and Data Privacy** – when GroupDocs.Merger accesses the network and what never leaves your machine: [Read the article →](https://docs.groupdocs.com/merger/net/network-access-and-data-privacy/)

## Keywords

`GroupDocs.Merger`, `.NET`, `C#`, `merge HTML documents`, `untrusted uploads`, `SSRF`, `external resources`, `IExternalResourceHandler`, `WhitelistedResources`, `LoadOptions`, `secure document merging`, `data leak prevention`

**Ready to get started?** [View Documentation](https://docs.groupdocs.com/merger/net/) | [Get Support](https://forum.groupdocs.com/c/merger/) | [Request License](https://purchase.groupdocs.com/temporary-license)
