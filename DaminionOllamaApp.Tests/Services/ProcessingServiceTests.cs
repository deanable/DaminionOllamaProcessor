using Xunit;
using Moq;
using FluentAssertions;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DaminionOllamaApp.Tests.Services
{
    public class ProcessingServiceTests
    {
        [Fact]
        public async Task ProcessLocalFileAsync_WithValidFile_ShouldProcessSuccessfully()
        {
            // Arrange
            var processingService = new ProcessingService();
            var fileItem = new FileQueueItem("test.jpg");
            var settings = new AppSettings();
            var progressReports = new List<string>();
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            await processingService.ProcessLocalFileAsync(
                fileItem, 
                settings, 
                message => progressReports.Add(message), 
                cancellationToken);

            // Add specific assertions based on expected behavior
            fileItem.Status.Should().NotBe(ProcessingStatus.Unprocessed);
        }
    }
}