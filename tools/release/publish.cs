// Adapted from Winter Sled's tools/release/publish.cs (MIT, (c) 2026 Roman Geraskin), itself adapted from
// Valheim World Editor's tools/release/publish.cs (MIT, (c) 2026 Thibault Bene).
// A release's steps that talk to the mod sites, for GitHub's release workflow (.github/workflows/release.yml;
// a .NET 10 file-based app: dotnet run --file tools/release/publish.cs -- ...; --file because the root holds a
// .csproj, which plain dotnet run would build instead). Run it from the repository's root: the
// sites' settings are in tools/release/sites.json.
//   notes <README.md> <version> <last tag, or -> <out.md>
//       The release notes: the lines of the README's "## Changelog" ("- **1.2.3** — ...") newer than the last
//       released tag, up to version.
//   thunderstore|nexus <dist> [--notes <notes.md>] [--dry-run]
//       Publishes dist/DraftAnimals-<version>.zip (./package.sh; the version is its manifest's) to one site.
//       --dry-run checks all it can without publishing: the token, that the version is not on the site yet,
//       the categories. A version already on the site fails: no site takes a version twice. --notes (Nexus
//       Mods): the release notes, added to the mod's changelog. Nexus Mods is skipped while sites.json has no
//       modFileId (the id of the mod's main file on Nexus).
// The tokens come from the environment: THUNDERSTORE_TOKEN, NEXUS_API_KEY.
#:property PublishAot=false
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string Repo = "https://github.com/fionov-a-v/Valheim-DraftAnimals";
const string Package = "DraftAnimals";
const string SitesFile = "tools/release/sites.json";

try
{
    switch (args.FirstOrDefault())
    {
        case "notes" when args.Length == 5:
            File.WriteAllText(args[4], Notes(File.ReadAllText(args[1]), args[2], args[3]), new UTF8Encoding(false));
            return 0;
        case "thunderstore" or "nexus" when args.Length >= 2:
            string[] options = args[2..];
            bool dryRun = options.Contains("--dry-run");
            int notesAt = Array.IndexOf(options, "--notes");
            string? notes = notesAt >= 0 && notesAt + 1 < options.Length ? File.ReadAllText(options[notesAt + 1]) : null;
            var site = JsonNode.Parse(File.ReadAllText(SitesFile))![args[0]]!;
            var release = FindRelease(args[1]);
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Package}-release/{release.Version}");
                switch (args[0])
                {
                    case "thunderstore":
                        await PublishThunderstore(http, site, release, dryRun);
                        break;
                    default:
                        await PublishNexus(http, site, release, notes, dryRun);
                        break;
                }
            }

            return 0;
        default:
            Console.Error.WriteLine("usage: publish.cs notes <README.md> <version> <last tag|-> <out.md>");
            Console.Error.WriteLine("       publish.cs thunderstore|nexus <dist> [--notes <notes.md>] [--dry-run]");
            return 2;
    }
}
catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or HttpRequestException or IOException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// The notes command: the README's "## Changelog" lines ("- **1.2.3** — ...", with their indented continuation
// lines) after lastTag (v1.2.3, or "-" for none) up to version, newest first, as they are.
static string Notes(string readme, string version, string lastTag)
{
    var current = Version.Parse(version);
    Version? last = lastTag == "-" ? null : Version.Parse(lastTag.TrimStart('v'));
    var log = Regex.Match(readme, @"^## Changelog[^\n]*\n(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
    if (!log.Success)
    {
        throw new InvalidDataException("README.md has no \"## Changelog\".");
    }

    var kept = new List<string>();
    foreach (string entry in Regex.Split(log.Groups[1].Value, "^(?=- )", RegexOptions.Multiline))
    {
        var m = Regex.Match(entry, @"^- \*\*v?([0-9]+\.[0-9]+\.[0-9]+)\*\*");
        if (!m.Success)
        {
            continue;
        }

        var v = Version.Parse(m.Groups[1].Value);
        if (v <= current && (last is null || v > last))
        {
            kept.Add(entry.TrimEnd());
        }
    }

    if (kept.Count == 0)
    {
        throw new InvalidDataException($"README.md's Changelog has no line for {version}.");
    }

    return string.Join("\n", kept) + $"\n\nFull list: [Changelog]({Repo}/blob/main/README.md#changelog).\n";
}

// The release's package in dist: DraftAnimals-<version>.zip, with its manifest and icon for the sites' checks.
static Release FindRelease(string dist)
{
    string[] zips = Directory.GetFiles(dist, $"{Package}-*.zip");
    if (zips.Length != 1)
    {
        throw new InvalidDataException($"{dist} must hold one {Package}-<version>.zip (it holds {zips.Length}).");
    }

    JsonNode manifest;
    byte[] manifestBytes, icon;
    using (var zip = ZipFile.OpenRead(zips[0]))
    {
        manifestBytes = ReadEntry(zip, "manifest.json");
        icon = ReadEntry(zip, "icon.png");
        manifest = JsonNode.Parse(manifestBytes)!;
    }

    var release = new Release((string)manifest["version_number"]!, (string)manifest["name"]!, zips[0], manifestBytes, icon);
    Console.WriteLine($"{release.Name} {release.Version}: {zips[0]}");
    return release;
}

static byte[] ReadEntry(ZipArchive zip, string name)
{
    var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The plugin zip has no {name}.");
    using var stream = entry.Open();
    using var copy = new MemoryStream();
    stream.CopyTo(copy);
    return copy.ToArray();
}

// ---- Thunderstore.

static async Task PublishThunderstore(HttpClient http, JsonNode site, Release release, bool dryRun)
{
    const string Name = "Thunderstore";
    string url = Text(site, "url"), team = Text(site, "team"), community = Text(site, "community");
    string[] categories = Texts(site, "categories");
    string token = Token("THUNDERSTORE_TOKEN");
    await NotThereYet(http, Name, $"{url}/api/experimental/package/{team}/{release.Name}/{release.Version}/");
    var user = await Call(http, Name, Get($"{url}/api/experimental/current-user/", Bearer(token)));
    if (user?["teams"]?.AsArray().Any(t => (string?)t == team) != true)
    {
        throw new InvalidOperationException($"{Name}: the token is not a service account of team {team}.");
    }

    await CategoriesExist(http, Name, $"{url}/api/experimental/community/{community}/category/", "slug", categories);
    // Thunderstore's own checks of the manifest and icon (its README check is broken on its side: always 500).
    var checks = new (string What, string Path, JsonObject Body)[]
    {
        ("manifest", "manifest-v1", new JsonObject { ["namespace"] = team, ["manifest_data"] = Convert.ToBase64String(release.Manifest) }),
        ("icon", "icon", new JsonObject { ["icon_data"] = Convert.ToBase64String(release.Icon) }),
    };
    foreach (var (what, path, body) in checks)
    {
        try
        {
            var answer = await Call(http, Name, Post($"{url}/api/experimental/submission/validate/{path}/", body, Bearer(token)));
            if (answer?["success"]?.GetValue<bool>() != true)
            {
                throw new InvalidOperationException($"{Name} refuses the package's {what}: {answer?.ToJsonString()}");
            }
        }
        catch (SiteException ex) when (ex.Status >= 500)
        {
            Console.WriteLine($"warning: {Name}'s {what} check is not working right now ({ex.Status}); not a verdict on the package.");
        }
    }

    Console.WriteLine($"{Name}: {team}-{release.Name} {release.Version} can be published (categories {string.Join(", ", categories)}).");
    if (dryRun)
    {
        return;
    }

    string upload = await Upload(http, Name, url, token, release.Zip);
    var result = await Submit(http, Name, url, token, upload, team, community, categories);
    Console.WriteLine($"{Name}: published {result?["package_version"]?["full_name"]}, {url}/c/{community}/p/{team}/{release.Name}/");
}

// The version must not be on the site: its page answers 404.
static async Task NotThereYet(HttpClient http, string site, string url)
{
    if (await Call(http, site, Get(url), missingIsNull: true) is not null)
    {
        throw new InvalidOperationException($"{site}: this version is published already ({url}). Raise the version: no site takes a version twice.");
    }
}

static async Task CategoriesExist(HttpClient http, string site, string url, string key, string[] wanted)
{
    var known = new HashSet<string>();
    for (string? page = url; page is not null;)
    {
        var answer = await Call(http, site, Get(page));
        foreach (var category in answer!["results"]!.AsArray())
        {
            known.Add((string)category![key]!);
        }

        page = (string?)(answer["pagination"]?["next_link"] ?? answer["next"]);
    }

    string[] missing = [.. wanted.Where(c => !known.Contains(c))];
    if (missing.Length > 0)
    {
        throw new InvalidOperationException($"{site} has no category {string.Join(", ", missing)} (tools/release/sites.json).");
    }
}

// Uploads the zip in the parts the site asks for and returns the upload's id; an upload that fails is called off.
// The parts go to presigned storage URLs, without the token.
static async Task<string> Upload(HttpClient http, string site, string url, string token, string zip)
{
    var begun = await Call(http, site, Post($"{url}/api/experimental/usermedia/initiate-upload/",
        new JsonObject { ["filename"] = Path.GetFileName(zip), ["file_size_bytes"] = new FileInfo(zip).Length }, Bearer(token)));
    string uuid = (string)begun!["user_media"]!["uuid"]!;
    try
    {
        var parts = new JsonArray();
        using (var file = File.OpenRead(zip))
        {
            foreach (var part in begun["upload_urls"]!.AsArray())
            {
                byte[] data = new byte[(int)part!["length"]!];
                file.Seek((long)part["offset"]!, SeekOrigin.Begin);
                file.ReadExactly(data);
                var target = new Uri(new Uri(url), (string)part["url"]!);
                using var put = new HttpRequestMessage(HttpMethod.Put, target) { Content = new ByteArrayContent(data) };

                using var answer = await http.SendAsync(put);
                if (!answer.IsSuccessStatusCode)
                {
                    throw new SiteException($"{site}: uploading part {part["part_number"]} failed ({(int)answer.StatusCode}).", (int)answer.StatusCode);
                }

                string etag = answer.Headers.TryGetValues("ETag", out var tags) ? tags.First() : "";
                parts.Add(new JsonObject { ["ETag"] = etag, ["PartNumber"] = (int)part["part_number"]! });
            }
        }

        await Call(http, site, Post($"{url}/api/experimental/usermedia/{uuid}/finish-upload/", new JsonObject { ["parts"] = parts }, Bearer(token)));
        return uuid;
    }
    catch (Exception ex) when (ex is SiteException or HttpRequestException or IOException)
    {
        try
        {
            await Call(http, site, Post($"{url}/api/experimental/usermedia/{uuid}/abort-upload/", new JsonObject(), Bearer(token)));
        }
        catch (Exception abort) when (abort is SiteException or HttpRequestException)
        {
            Console.WriteLine($"warning: {site}: calling off the upload failed: {abort.Message}");
        }

        throw;
    }
}

// Submits an uploaded zip: the site checks and publishes it before answering.
static async Task<JsonNode?> Submit(HttpClient http, string site, string url, string token, string upload, string team, string community, string[] categories)
{
    var body = new JsonObject
    {
        ["upload_uuid"] = upload,
        ["author_name"] = team,
        ["communities"] = new JsonArray(community),
        ["community_categories"] = new JsonObject { [community] = new JsonArray([.. categories.Select(c => JsonValue.Create(c))]) },
        ["has_nsfw_content"] = false,
    };
    return await Call(http, site, Post($"{url}/api/experimental/submission/submit/", body, Bearer(token)));
}

// ---- Nexus Mods: a new version of each of the mod's existing files (v3 API, as Nexus-Mods/upload-action).

static async Task PublishNexus(HttpClient http, JsonNode site, Release release, string? notes, bool dryRun)
{
    const string Name = "Nexus Mods";
    string url = Text(site, "url"), game = Text(site, "game");
    int modId = (int?)site["modId"] ?? 0;
    string modFile = (string?)site["modFileId"] ?? "";
    if (modId == 0 || modFile.Length == 0)
    {
        Console.WriteLine($"{Name}: no modId or modFileId in tools/release/sites.json yet: skipped. Fill in the id of the mod's main file.");
        return;
    }

    string key = Token("NEXUS_API_KEY");
    (string, string)[] auth = [("apikey", key), ("Application-Name", $"{Package}-release"), ("Application-Version", release.Version)];
    var user = await Call(http, Name, Get($"{url}/v1/users/validate.json", auth));
    Console.WriteLine($"{Name}: the key is {user?["name"]}'s.");
    string modUid = (string)(await V3(http, Get($"{url}/v3/games/{game}/mods/{modId}", auth)))!["id"]!;
    // the mod page: the BBCode description kept in the repository and the manifest's one-line description
    string? pageFile = (string?)site["pageDescription"];
    string? page = pageFile is not null ? File.ReadAllText(pageFile) : null;
    string summary = (string)JsonNode.Parse(release.Manifest)!["description"]!;
    if (page is not null)
    {
        Console.WriteLine($"{Name}: the mod page gets {pageFile} ({page.Length} characters) and the summary \"{summary}\".");
    }

    if (await HasVersion(http, url, auth, modFile, release.Version))
    {
        // uploaded by hand (the first release) or by an earlier run that stopped part way
        Console.WriteLine($"{Name}: file {modFile} has version {release.Version} already: skipped.");
        if (!dryRun)
        {
            await UpdateNexusPage(http, url, auth, modUid, page, summary);
        }

        return;
    }

    // Nexus takes .zip, .rar and .7z only; the plugin goes where Vortex and a hand install put it.
    string work = Directory.CreateTempSubdirectory("nexus-").FullName;
    string archive = NexusPlugin(release.Zip, release.Name, Path.Combine(work, Path.GetFileName(release.Zip)));
    Console.WriteLine($"{Name}: {Path.GetFileName(archive)} ({new FileInfo(archive).Length / 1024} KiB) → file {modFile}.");
    if (dryRun)
    {
        Console.WriteLine($"{Name}: mod {modId} can take it.");
        return;
    }

    var body = new JsonObject
    {
        ["upload_id"] = await NexusUpload(http, url, auth, archive),
        ["name"] = Template(site, "name", release.Version),
        ["description"] = Template(site, "description", release.Version),
        ["version"] = release.Version,
        ["file_category"] = "main",
        ["update_mod_version"] = true,
        ["allow_mod_manager_download"] = true,
        ["archive_existing_file"] = true,
    };
    await V3(http, Post($"{url}/v3/mod-files/{modFile}/versions", body, auth));
    Console.WriteLine($"{Name}: published {body["name"]}.");
    if (notes is not null)
    {
        await V3(http, Post($"{url}/v3/mods/{modUid}/changelogs", new JsonObject { ["version"] = release.Version, ["changelog"] = PlainText(notes) }, auth));
        Console.WriteLine($"{Name}: changelog added.");
    }

    await UpdateNexusPage(http, url, auth, modUid, page, summary);
    Console.WriteLine($"{Name}: https://www.nexusmods.com/{game}/mods/{modId}?tab=files");
}

// The mod page's description (BBCode) and summary, as the page editor would save them.
static async Task UpdateNexusPage(HttpClient http, string url, (string, string)[] auth, string modUid, string? page, string summary)
{
    if (page is null)
    {
        return;
    }

    await V3(http, Request(HttpMethod.Patch, $"{url}/v3/mods/{modUid}", new JsonObject { ["description"] = page, ["summary"] = summary }, auth));
    Console.WriteLine("Nexus Mods: the mod page's description and summary updated.");
}

// Whether a Nexus file has this version already (not removed or archived).
static async Task<bool> HasVersion(HttpClient http, string url, (string, string)[] auth, string modFile, string version)
{
    var versions = (await V3(http, Get($"{url}/v3/mod-files/{modFile}/versions", auth)))!["versions"]!.AsArray();
    return versions.Any(v => (string?)v!["version"] == version && (string?)v["category"] is not ("removed" or "archived"));
}

// Uploads a file and waits until Nexus has processed it; returns the upload's id. Up to 100 MiB in one part: its
// presigned URL signs the Content-Type, the Content-Disposition (with the name given) and the Content-MD5, so the
// PUT sends exactly these. Bigger files go in the parts Nexus asks for.
static async Task<string> NexusUpload(HttpClient http, string url, (string, string)[] auth, string path)
{
    const long SinglePartLimit = 100 * 1024 * 1024;
    string name = Path.GetFileName(path);
    long size = new FileInfo(path).Length;
    JsonNode upload;
    if (size <= SinglePartLimit)
    {
        byte[] data = await File.ReadAllBytesAsync(path);
        // Nexus asks for the MD5 (a checksum here, not security).
#pragma warning disable CA5351
        byte[] md5 = MD5.HashData(data);
#pragma warning restore CA5351
        upload = (await V3(http, Post($"{url}/v3/uploads", new JsonObject { ["filename"] = name, ["size_bytes"] = size, ["md5"] = Convert.ToHexStringLower(md5) }, auth)))!;
        using var put = new HttpRequestMessage(HttpMethod.Put, (string)upload["presigned_url"]!) { Content = new ByteArrayContent(data) };
        put.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        put.Content.Headers.TryAddWithoutValidation("Content-Disposition", $"attachment; filename=\"{name}\"");
        put.Content.Headers.ContentMD5 = md5;
        await Stored(http, put, name);
    }
    else
    {
        upload = (await V3(http, Post($"{url}/v3/uploads/multipart", new JsonObject { ["filename"] = name, ["size_bytes"] = size.ToString(System.Globalization.CultureInfo.InvariantCulture) }, auth)))!;
        long partSize = long.Parse((string)upload["part_size_bytes"]!, System.Globalization.CultureInfo.InvariantCulture);
        var xml = new StringBuilder("<CompleteMultipartUpload>");
        using (var file = File.OpenRead(path))
        {
            int number = 0;
            foreach (var partUrl in upload["part_presigned_urls"]!.AsArray())
            {
                number++;
                byte[] data = new byte[(int)Math.Min(partSize, size - ((number - 1) * partSize))];
                file.Seek((number - 1) * partSize, SeekOrigin.Begin);
                file.ReadExactly(data);
                using var put = new HttpRequestMessage(HttpMethod.Put, (string)partUrl!) { Content = new ByteArrayContent(data) };
                put.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                string etag = await Stored(http, put, $"{name} part {number}");
                xml.Append(System.Globalization.CultureInfo.InvariantCulture, $"<Part><PartNumber>{number}</PartNumber><ETag>{etag.Replace("\"", "", StringComparison.Ordinal)}</ETag></Part>");
            }
        }

        using var complete = new HttpRequestMessage(HttpMethod.Post, (string)upload["complete_presigned_url"]!)
        {
            Content = new StringContent(xml.Append("</CompleteMultipartUpload>").ToString(), Encoding.UTF8, "application/xml"),
        };
        await Stored(http, complete, $"{name} (completing the parts)");
    }

    string id = (string)upload["id"]!;
    await V3(http, Post($"{url}/v3/uploads/{id}/finalise", new JsonObject(), auth));
    var waited = TimeSpan.Zero;
    for (var delay = TimeSpan.FromSeconds(2); ; delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30)))
    {
        string? state = (string?)(await V3(http, Get($"{url}/v3/uploads/{id}", auth)))?["state"];
        if (state == "available")
        {
            return id;
        }

        if (waited > TimeSpan.FromMinutes(10))
        {
            throw new InvalidOperationException($"Nexus Mods was still processing {name} after 10 minutes (state {state}).");
        }

        await Task.Delay(delay);
        waited += delay;
    }
}

// Sends a request to Nexus' storage; returns the stored part's ETag.
static async Task<string> Stored(HttpClient http, HttpRequestMessage request, string what)
{
    using var answer = await http.SendAsync(request);
    if (!answer.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"Nexus Mods: storing {what} failed ({(int)answer.StatusCode}): {Shorten(await answer.Content.ReadAsStringAsync())}");
    }

    return answer.Headers.TryGetValues("ETag", out var tags) ? tags.First() : "";
}

// A v3 call: the answer's payload, out of its {"data": ...}.
static async Task<JsonNode?> V3(HttpClient http, HttpRequestMessage request)
{
    var answer = await Call(http, "Nexus Mods", request);
    return answer is JsonObject o && o.ContainsKey("data") ? o["data"] : answer;
}

// The plugin's zip for Nexus: its files under BepInEx/plugins/<name>/ (no manifest or icon: Nexus does not use them).
static string NexusPlugin(string zip, string name, string output)
{
    using var source = ZipFile.OpenRead(zip);
    using var target = ZipFile.Open(output, ZipArchiveMode.Create);
    foreach (var entry in source.Entries.Where(e => !e.FullName.EndsWith('/') && e.FullName is not ("manifest.json" or "icon.png")))
    {
        string path = entry.FullName.StartsWith("plugins/", StringComparison.Ordinal) ? entry.FullName["plugins/".Length..] : entry.FullName;
        var copy = target.CreateEntry($"BepInEx/plugins/{name}/{path}", CompressionLevel.Optimal);
        copy.LastWriteTime = entry.LastWriteTime;
        using var from = entry.Open();
        using var to = copy.Open();
        from.CopyTo(to);
    }

    return output;
}

// Markdown as plain text, for Nexus' changelog (links keep their text, no ** or `).
static string PlainText(string markdown)
{
    string text = Regex.Replace(markdown, @"\[([^\]]+)\]\([^)]+\)", "$1");
    text = Regex.Replace(text, "^#+ ", "", RegexOptions.Multiline);
    return text.Replace("**", "", StringComparison.Ordinal).Replace("`", "", StringComparison.Ordinal).Trim();
}

static string Template(JsonNode file, string key, string version) => Text(file, key).Replace("{version}", version, StringComparison.Ordinal);

// ---- Requests.

static HttpRequestMessage Get(string url, params (string Name, string Value)[] headers) => Request(HttpMethod.Get, url, null, headers);

static HttpRequestMessage Post(string url, JsonNode body, params (string Name, string Value)[] headers) => Request(HttpMethod.Post, url, body, headers);

static HttpRequestMessage Request(HttpMethod method, string url, JsonNode? body, (string Name, string Value)[] headers)
{
    var request = new HttpRequestMessage(method, url);
    if (body is not null)
    {
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
    }

    foreach (var (name, value) in headers)
    {
        request.Headers.TryAddWithoutValidation(name, value);
    }

    return request;
}

static (string, string) Bearer(string token) => ("Authorization", $"Bearer {token}");

// Sends a request and returns the JSON it answers (null for none). An error answer throws SiteException with
// what the site said; missingIsNull: a 404 answers null instead.
static async Task<JsonNode?> Call(HttpClient http, string site, HttpRequestMessage request, bool missingIsNull = false)
{
    using (request)
    {
        using var answer = await http.SendAsync(request);
        string text = await answer.Content.ReadAsStringAsync();
        if (missingIsNull && answer.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!answer.IsSuccessStatusCode)
        {
            // The URL without its query: a presigned URL's query is a credential.
            string where = request.RequestUri!.GetLeftPart(UriPartial.Path);
            throw new SiteException($"{site} answered {(int)answer.StatusCode} to {request.Method} {where}: {Shorten(text)}", (int)answer.StatusCode);
        }

        return text.Length == 0 ? null : JsonNode.Parse(text);
    }
}

static string Shorten(string text) => text.Length > 500 ? text[..500] + "…" : text;

static string Token(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } token
    ? token
    : throw new InvalidOperationException($"{name} is not set (GitHub: the repository's secrets).");

static string Text(JsonNode node, string key) => (string?)node[key] ?? throw new InvalidDataException($"tools/release/sites.json: no {key}.");

static string[] Texts(JsonNode node, string key) => [.. node[key]!.AsArray().Select(v => (string)v!)];

// The release's package: the plugin's zip, with its manifest and icon for the sites' checks.
internal sealed record Release(string Version, string Name, string Zip, byte[] Manifest, byte[] Icon);

// A site's error answer, with its HTTP status.
internal sealed class SiteException(string message, int status) : InvalidOperationException(message)
{
    public int Status { get; } = status;
}
