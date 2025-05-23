// DaminionOllamaWpfApp/Services/ImageMetadataService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;
using System.Xml.Linq;
using DaminionOllamaInteractionLib.Ollama;

namespace DaminionOllamaInteractionLib.Services
{
    /// <summary>
    /// Service for reading and writing image metadata using ImageMagick.
    /// </summary>
    public class ImageMetadataService : IDisposable
    {
        private readonly string _filePath;
        private MagickImage? _image;
        private IExifProfile? _exifProfile;
        private IIptcProfile? _iptcProfile;
        private IXmpProfile? _xmpProfile;

        private static readonly XNamespace RdfNS = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace DcNS = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace XmpMetaNS = "adobe:ns:meta/";

        public string? Description { get; set; }
        public List<string> Keywords { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public string? ExifImageDescription { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMetadataService"/> class.
        /// </summary>
        /// <param name="filePath"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ImageMetadataService(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// Reads the image metadata from the specified file.
        /// </summary>
        public void Read()
        {
            _image?.Dispose();
            _image = new MagickImage(_filePath);

            _exifProfile = _image.GetExifProfile();
            _iptcProfile = _image.GetIptcProfile();
            _xmpProfile = _image.GetXmpProfile();

            PopulateMainProperties();
        }

        /// <summary>
        /// Saves the image metadata to the specified file.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Save()
        {
            if (_image == null)
                throw new InvalidOperationException("Image has not been read. Call Read() first.");

            UpdateMainProperties();

            if (_exifProfile is { Values.Count: > 0 })
                _image.SetProfile(_exifProfile);
            else
                _image.RemoveProfile("exif");

            if (_iptcProfile is { Values.Count: > 0 })
                _image.SetProfile(_iptcProfile);
            else
                _image.RemoveProfile("iptc");

            if (_xmpProfile != null)
            {
                var xdoc = TryGetXmpDocument(_xmpProfile);
                var rdfDesc = xdoc?.Root?.Element(RdfNS + "RDF")?.Element(RdfNS + "Description");

                bool hasContent = rdfDesc?.Elements().Any(el =>
                    el.Name.Namespace != XmpMetaNS &&
                    el.Name.Namespace != RdfNS &&
                    !el.Name.LocalName.StartsWith("xmlns", StringComparison.Ordinal)) ?? false;

                if (hasContent)
                    _image.SetProfile(_xmpProfile);
                else
                    _image.RemoveProfile("xmp");
            }

            _image.Write(_filePath);
        }

        /// <summary>
        /// Populates the metadata properties from the parsed Ollama content.
        /// </summary>
        /// <param name="content"></param>
        public void PopulateFromOllamaContent(ParsedOllamaContent content)
        {
            Description = content.Description;
            Keywords = new List<string>(content.Keywords);
            Categories = new List<string>(content.Categories);
            ExifImageDescription = content.Description;
        }

        /// <summary>
        /// Populates the main properties from the XMP, IPTC, and EXIF profiles.
        /// </summary>
        private void PopulateMainProperties()
        {
            Description = GetXmpSimpleString(DcNS + "description")
                          ?? GetIptcSingleValue(IptcTag.Caption)
                          ?? GetExifStringValue(ExifTag.ImageDescription);

            ExifImageDescription = GetExifStringValue(ExifTag.ImageDescription);

            Keywords = GetIptcMultipleValues(IptcTag.Keyword)
                .Concat(GetXmpBag(DcNS + "subject"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Categories = GetXmpBag(DcNS + "type");
        }

        /// <summary>
        /// Updates the main properties in the XMP, IPTC, and EXIF profiles.
        /// </summary>
        private void UpdateMainProperties()
        {
            SetXmpSimpleString(DcNS + "description", Description);
            SetIptcSingleValue(IptcTag.Caption, Description);
            SetExifStringValue(ExifTag.ImageDescription, ExifImageDescription);
            SetIptcMultipleValues(IptcTag.Keyword, Keywords);
            SetXmpBag(DcNS + "subject", Keywords);
            SetXmpBag(DcNS + "type", Categories);
        }

        /// <summary>
        /// Gets the EXIF string value for the specified tag identifier.
        /// </summary>
        /// <param name="tagIdentifier"></param>
        /// <returns></returns>
        private string? GetExifStringValue(ExifTag tagIdentifier)
        {
            if (_exifProfile == null) return null;

            if (tagIdentifier == ExifTag.ImageDescription)
                return _exifProfile.GetValue(ExifTag<string>.ImageDescription)?.Value;

            return null;
        }

        /// <summary>
        /// Sets the EXIF string value for the specified tag identifier.
        /// </summary>
        /// <param name="tagIdentifier"></param>
        /// <param name="value"></param>
        private void SetExifStringValue(ExifTag tagIdentifier, string? value)
        {
            if (tagIdentifier != ExifTag.ImageDescription) return;

            _exifProfile ??= new ExifProfile();

            if (string.IsNullOrEmpty(value))
                _exifProfile.RemoveValue(ExifTag<string>.ImageDescription);
            else
                _exifProfile.SetValue(ExifTag<string>.ImageDescription, value);
        }

        /// <summary>
        /// Gets the IPTC single value for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private string? GetIptcSingleValue(IptcTag tag)
        {
            return _iptcProfile?.Values.FirstOrDefault(v => v.Tag == tag)?.Value;
        }

        /// <summary>
        ///     Gets the IPTC multiple values for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private List<string> GetIptcMultipleValues(IptcTag tag)
        {
            return _iptcProfile?.Values
                       .Where(v => v.Tag == tag && !string.IsNullOrEmpty(v.Value))
                       .Select(v => v.Value)
                       .ToList() ?? new List<string>();
        }

        /// <summary>
        /// Sets the IPTC single value for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        private void SetIptcSingleValue(IptcTag tag, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;

            _iptcProfile ??= new IptcProfile();
            _iptcProfile.RemoveValue(tag);
            _iptcProfile.SetValue(tag, value);
        }

        /// <summary>
        /// Sets the IPTC multiple values for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="values"></param>
        private void SetIptcMultipleValues(IptcTag tag, List<string> values)
        {
            if (values == null || values.All(string.IsNullOrWhiteSpace)) return;

            _iptcProfile ??= new IptcProfile();
            _iptcProfile.RemoveValue(tag);

            foreach (var val in values.Where(s => !string.IsNullOrWhiteSpace(s)))
                _iptcProfile.SetValue(tag, val);
        }

        /// <summary>
        ///     Tries to get the XMP document from the profile.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        private XDocument? TryGetXmpDocument(IXmpProfile? profile)
        {
            try { return profile?.ToXDocument(); }
            catch (Exception ex) { Console.WriteLine($"Failed to parse XMP: {ex.Message}"); return null; }
        }

        /// <summary>
        /// Creates a new XMP document if it doesn't exist.
        /// </summary>
        /// <returns></returns>
        private XDocument GetOrCreateXmpDocument()
        {
            var xdoc = TryGetXmpDocument(_xmpProfile);
            if (xdoc != null) return xdoc;

            var newDoc = new XDocument(
                new XElement(XmpMetaNS + "xmpmeta",
                    new XAttribute(XNamespace.Xmlns + "x", XmpMetaNS.NamespaceName),
                    new XElement(RdfNS + "RDF",
                        new XAttribute(XNamespace.Xmlns + "rdf", RdfNS.NamespaceName),
                        new XElement(RdfNS + "Description",
                            new XAttribute(RdfNS + "about", ""),
                            new XAttribute(XNamespace.Xmlns + "dc", DcNS.NamespaceName)))));

            try { _xmpProfile = XmpProfile.FromXDocument(newDoc); }
            catch (Exception ex) { Console.WriteLine($"Failed to create XMP profile: {ex.Message}"); }

            return newDoc;
        }

        /// <summary>
        /// Updates the XMP profile from the XDocument.
        /// </summary>
        /// <param name="xdoc"></param>
        private void UpdateXmpProfileFromDocument(XDocument xdoc)
        {
            try { _xmpProfile = XmpProfile.FromXDocument(xdoc); }
            catch (Exception ex) { Console.WriteLine($"Failed to update XMP profile: {ex.Message}"); }
        }

        /// <summary>
        /// Gets or creates the RDF description element in the XMP document.
        /// </summary>
        /// <param name="xdoc"></param>
        /// <returns></returns>
        private static XElement GetOrCreateRdfDescription(XDocument xdoc)
        {
            var root = xdoc.Root ?? new XElement(XmpMetaNS + "xmpmeta");
            if (xdoc.Root == null) xdoc.Add(root);

            var rdf = root.Element(RdfNS + "RDF") ?? new XElement(RdfNS + "RDF");
            if (root.Element(RdfNS + "RDF") == null) root.Add(rdf);

            var desc = rdf.Element(RdfNS + "Description") ?? new XElement(RdfNS + "Description");
            if (rdf.Element(RdfNS + "Description") == null) rdf.Add(desc);

            if (desc.Attribute(XNamespace.Xmlns + "dc") == null)
                desc.SetAttributeValue(XNamespace.Xmlns + "dc", DcNS.NamespaceName);

            return desc;
        }

        /// <summary>
        /// Gets the XMP simple string value for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private string? GetXmpSimpleString(XName tag)
        {
            var xdoc = TryGetXmpDocument(_xmpProfile ?? _image?.GetXmpProfile());
            if (xdoc == null) return null;

            var rdf = xdoc.Root?.Element(RdfNS + "RDF")?.Element(RdfNS + "Description");
            var element = rdf?.Element(tag);

            if (element != null) return element.Value;

            if (tag == DcNS + "description")
                return rdf?.Element(tag)?.Element(RdfNS + "Alt")?.Elements(RdfNS + "li")
                    .FirstOrDefault(li => (string?)li.Attribute(XNamespace.Xml + "lang") == "x-default")?.Value;

            return null;
        }

        /// <summary>
        /// Sets the XMP simple string value for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        private void SetXmpSimpleString(XName tag, string? value)
        {
            var xdoc = GetOrCreateXmpDocument();
            var desc = GetOrCreateRdfDescription(xdoc);

            desc.Element(tag)?.Remove();

            if (!string.IsNullOrEmpty(value))
            {
                if (tag == DcNS + "description")
                {
                    desc.Add(new XElement(tag,
                        new XElement(RdfNS + "Alt",
                            new XElement(RdfNS + "li", value, new XAttribute(XNamespace.Xml + "lang", "x-default")))));
                }
                else
                {
                    desc.Add(new XElement(tag, value));
                }
            }

            UpdateXmpProfileFromDocument(xdoc);
        }

        /// <summary>
        /// Gets the XMP bag (multiple values) for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private List<string> GetXmpBag(XName tag)
        {
            var xdoc = TryGetXmpDocument(_xmpProfile ?? _image?.GetXmpProfile());
            var bag = xdoc?.Root?.Element(RdfNS + "RDF")?.Element(RdfNS + "Description")?.Element(tag)?.Element(RdfNS + "Bag");

            return bag?.Elements(RdfNS + "li").Select(li => li.Value).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Sets the XMP bag (multiple values) for the specified tag.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="values"></param>
        private void SetXmpBag(XName tag, List<string> values)
        {
            if (values == null || values.All(string.IsNullOrWhiteSpace)) return;

            var xdoc = GetOrCreateXmpDocument();
            var desc = GetOrCreateRdfDescription(xdoc);
            desc.Element(tag)?.Remove();

            var bag = new XElement(RdfNS + "Bag");
            foreach (var val in values.Where(s => !string.IsNullOrWhiteSpace(s)))
                bag.Add(new XElement(RdfNS + "li", val));

            desc.Add(new XElement(tag, bag));
            UpdateXmpProfileFromDocument(xdoc);
        }

        private bool _disposed;
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
                _image?.Dispose();
            _disposed = true;
        }
        ~ImageMetadataService() => Dispose(false);
    }
}
