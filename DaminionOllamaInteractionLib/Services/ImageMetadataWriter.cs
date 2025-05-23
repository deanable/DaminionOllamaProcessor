// DaminionOllamaWpfApp/Services/ImageMetadataWriter.cs
using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// using System.Xml.Linq; // We'll simplify XMP and might not need direct XDocument manipulation for now
using DaminionOllamaInteractionLib.Ollama;

namespace DaminionOllamaInteractionLib.Services
{
    /// <summary>
    /// Handles writing metadata to images using ImageMagick.
    /// </summary>
    public static class ImageMetadataWriter
    {
        /// <summary>
        /// Writes metadata to an image file.
        /// </summary>
        /// <param name="imagePath"></param>
        /// <param name="ollamaContent"></param>
        /// <returns></returns>
        public static bool WriteMetadataToImage(string imagePath, ParsedOllamaContent ollamaContent)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath) || ollamaContent == null)
            {
                Console.Error.WriteLine("[ImageMetadataWriter] Error: Invalid image path or Ollama content.");
                return false;
            }

            try
            {
                Console.WriteLine($"[ImageMetadataWriter] Attempting to write metadata to: {imagePath}");

                using (MagickImage image = new MagickImage(imagePath))
                {
                    bool changesMadeToImage = false;

                    // --- IPTC Profile ---
                    IIptcProfile? iptcProfile = image.GetIptcProfile(); // Gets existing or returns null
                    bool iptcProfileWasNewlyCreated = false;

                    // Create a new profile only if there's actual content to write and no profile exists
                    if (iptcProfile == null &&
                        (!string.IsNullOrWhiteSpace(ollamaContent.Description) || ollamaContent.Keywords.Any() || ollamaContent.Categories.Any()))
                    {
                        Console.WriteLine("[ImageMetadataWriter] No IPTC profile found, creating a new one.");
                        iptcProfile = new IptcProfile();
                        iptcProfileWasNewlyCreated = true;
                    }

                    if (iptcProfile != null)
                    {
                        bool currentIptcProfileChanged = false;
                        if (!string.IsNullOrWhiteSpace(ollamaContent.Description))
                        {
                            iptcProfile.SetValue(IptcTag.Caption, ollamaContent.Description);
                            Console.WriteLine($"[ImageMetadataWriter] Set IPTC Caption.");
                            currentIptcProfileChanged = true;
                        }

                        if (ollamaContent.Keywords.Any())
                        {
                            iptcProfile.RemoveValue(IptcTag.Keyword);
                            foreach (string keyword in ollamaContent.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                            {
                                iptcProfile.SetValue(IptcTag.Keyword, keyword);
                            }
                            Console.WriteLine($"[ImageMetadataWriter] Set IPTC Keywords.");
                            currentIptcProfileChanged = true;
                        }

                        if (ollamaContent.Categories.Any())
                        {
                            iptcProfile.RemoveValue(IptcTag.Category);
                            foreach (string category in ollamaContent.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                            {
                                iptcProfile.SetValue(IptcTag.Category, category);
                            }
                            Console.WriteLine($"[ImageMetadataWriter] Set IPTC Categories.");
                            currentIptcProfileChanged = true;
                        }

                        if (currentIptcProfileChanged || iptcProfileWasNewlyCreated)
                        {
                            image.SetProfile(iptcProfile);
                            changesMadeToImage = true;
                        }
                    }

                    // --- Simplified XMP Profile Handling ---
                    // We will only try to add a new XMP profile with a description if one doesn't exist
                    // and if there's a description to add.
                    // Modifying existing complex XMP is deferred.
                    if (!string.IsNullOrWhiteSpace(ollamaContent.Description))
                    {
                        IXmpProfile? xmpProfile = image.GetXmpProfile();
                        if (xmpProfile == null)
                        {
                            Console.WriteLine("[ImageMetadataWriter] No XMP profile found. Attempting to create a new one for description.");
                            // Create a minimal XMP packet string that includes dc:description
                            // Ensure namespaces are correctly defined and used.
                            string minimalXmpPacket = $@"<?xpacket begin="""" id=""W5M0MpCehiHzreSzNTczkc9d""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"" x:xmptk=""ImageMagick"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"">
      <dc:description>
        <rdf:Alt>
          <rdf:li xml:lang=""x-default"">{System.Security.SecurityElement.Escape(ollamaContent.Description)}</rdf:li>
        </rdf:Alt>
      </dc:description>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>
<?xpacket end=""w""?>";
                            try
                            {
                                xmpProfile = new XmpProfile(minimalXmpPacket); // Create from string
                                image.SetProfile(xmpProfile); // Use SetProfile to add it
                                changesMadeToImage = true;
                                Console.WriteLine("[ImageMetadataWriter] Added new XMP profile with dc:description.");
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"[ImageMetadataWriter] Error creating or setting new XMP profile: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ImageMetadataWriter] Existing XMP profile found. Advanced modification of existing XMP is not implemented in this simplified version.");
                            // If you wanted to *modify* existing XMP, you would use:
                            // byte[]? xmpData = xmpProfile.ToByteArray(); // Corrected from GetData()
                            // if (xmpData != null) { /* Parse with XDocument, modify, create new XmpProfile(modifiedBytes), image.SetProfile() */ }
                        }
                    }


                    if (changesMadeToImage)
                    {
                        Console.WriteLine($"[ImageMetadataWriter] Writing changes to {imagePath}");
                        image.Write(imagePath);
                        Console.WriteLine($"[ImageMetadataWriter] Successfully wrote metadata changes to {imagePath}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[ImageMetadataWriter] No new metadata changes to write to {imagePath}");
                        return true;
                    }
                }
            }
            catch (MagickException magickEx)
            {
                Console.Error.WriteLine($"[ImageMetadataWriter] Magick.NET error writing metadata to {imagePath}: {magickEx.Message}\n{magickEx.StackTrace}");
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ImageMetadataWriter] General error writing metadata to {imagePath}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}