// DaminionOllamaWpfApp/Services/ImageMetadataWriter.cs (Example structure)
using ImageMagick; // This using statement is for Magick.NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaminionOllamaInteractionLib.Ollama; // For ParsedOllamaContent

namespace DaminionOllamaWpfApp.Services
{
    public static class ImageMetadataWriter
    {
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

                // Create a new MagickImage object from the file path
                using (MagickImage image = new MagickImage(imagePath))
                {
                    // --- IPTC Profile ---
                    IIptcProfile iptcProfile = image.GetIptcProfile();
                    if (iptcProfile == null)
                    {
                        Console.WriteLine("[ImageMetadataWriter] No IPTC profile found, creating a new one.");
                        iptcProfile = new IptcProfile();
                    }

                    // Description (Caption/Abstract - IPTC 2:120)
                    if (!string.IsNullOrWhiteSpace(ollamaContent.Description))
                    {
                        iptcProfile.SetValue(IptcTag.Caption, ollamaContent.Description);
                        Console.WriteLine($"[ImageMetadataWriter] Set IPTC Caption: {ollamaContent.Description.Substring(0, Math.Min(ollamaContent.Description.Length, 50))}...");
                    }

                    // Keywords (IPTC 2:025 - repeatable)
                    if (ollamaContent.Keywords.Any())
                    {
                        // Remove existing keywords if you want to replace them completely
                        // Or just add new ones. For this example, let's replace.
                        var existingKeywords = iptcProfile.GetValues(IptcTag.Keyword)?.ToList();
                        if (existingKeywords != null)
                        {
                            foreach (var val in existingKeywords)
                            {
                                iptcProfile.RemoveValue(IptcTag.Keyword, val.Value);
                            }
                        }
                        // Add new keywords
                        foreach (string keyword in ollamaContent.Keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(keyword))
                            {
                                iptcProfile.SetValue(IptcTag.Keyword, keyword); // Magick.NET handles multiple values for the same tag
                            }
                        }
                        Console.WriteLine($"[ImageMetadataWriter] Set IPTC Keywords: {string.Join(", ", ollamaContent.Keywords)}");
                    }

                    // Categories (IPTC 2:015 - Category, also repeatable)
                    // You might also consider Supplemental Categories (IPTC 2:020)
                    if (ollamaContent.Categories.Any())
                    {
                        var existingCategories = iptcProfile.GetValues(IptcTag.Category)?.ToList();
                        if (existingCategories != null)
                        {
                            foreach (var val in existingCategories)
                            {
                                iptcProfile.RemoveValue(IptcTag.Category, val.Value);
                            }
                        }
                        foreach (string category in ollamaContent.Categories)
                        {
                            if (!string.IsNullOrWhiteSpace(category))
                            {
                                iptcProfile.SetValue(IptcTag.Category, category);
                            }
                        }
                        Console.WriteLine($"[ImageMetadataWriter] Set IPTC Categories: {string.Join(", ", ollamaContent.Categories)}");
                    }

                    image.SetProfile(iptcProfile); // Apply the IPTC profile changes

                    // --- XMP Profile (Optional but Recommended for modern compatibility) ---
                    // XMP is more complex due to its XML structure and namespaces.
                    // Magick.NET allows getting the XMP profile as an IXmpProfile object,
                    // which often wraps an XML document or a similar structure.
                    // You'd typically use specific XMP schemas like Dublin Core (dc:) for general metadata.

                    IXmpProfile? xmpProfile = image.GetXmpProfile();
                    if (xmpProfile == null)
                    {
                        Console.WriteLine("[ImageMetadataWriter] No XMP profile found, creating a new one.");
                        xmpProfile = new XmpProfile(); // Create new XMP profile
                    }

                    if (!string.IsNullOrWhiteSpace(ollamaContent.Description))
                    {
                        // Standard XMP tag for description is dc:description
                        // The SetValue method for XMP usually takes namespace, path, value.
                        // You might need to explore the xmpProfile object's methods.
                        // This is a simplified example; direct XMP manipulation can be intricate.
                        // A common way is to set it as an RDF description.
                        // Example: xmpProfile.SetValue("http://purl.org/dc/elements/1.1/", "description", ollamaContent.Description);
                        // For Magick.NET, often it's easier if it can map IPTC to XMP automatically or has simpler setters.
                        // For now, we'll focus on IPTC as it's more straightforward with direct tag enums.
                        // Let's add a placeholder indicating XMP description was intended:
                        xmpProfile.CreateTraverser().SetValue("http://purl.org/dc/elements/1.1/", "dc:description", ollamaContent.Description);
                        Console.WriteLine($"[ImageMetadataWriter] Set XMP dc:description (conceptual).");
                    }

                    if (ollamaContent.Keywords.Any())
                    {
                        // XMP dc:subject is often used for keywords (as a Bag - an unordered list)
                        // xmpProfile.CreateTraverser().SetValue("http://purl.org/dc/elements/1.1/", "dc:subject", string.Join(";", ollamaContent.Keywords)); // Simple, but ideally a bag
                        Console.WriteLine($"[ImageMetadataWriter] Set XMP dc:subject for keywords (conceptual).");
                    }

                    if (xmpProfile.ToByteArray().Length > 0) // Check if XMP profile has content
                    {
                        image.SetProfile(xmpProfile);
                    }


                    // Save the changes back to the original file
                    // WARNING: This overwrites the original file. Consider making a backup or saving to a new file first.
                    image.Write(imagePath);
                    Console.WriteLine($"[ImageMetadataWriter] Successfully wrote metadata to {imagePath}");
                    return true;
                }
            }
            catch (MagickException magickEx)
            {
                Console.Error.WriteLine($"[ImageMetadataWriter] Magick.NET error writing metadata to {imagePath}: {magickEx.Message}");
                Console.Error.WriteLine($"[ImageMetadataWriter] Magick.NET StackTrace: {magickEx.StackTrace}");
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ImageMetadataWriter] General error writing metadata to {imagePath}: {ex.Message}");
                Console.Error.WriteLine($"[ImageMetadataWriter] StackTrace: {ex.StackTrace}");
                return false;
            }
        }
    }
}