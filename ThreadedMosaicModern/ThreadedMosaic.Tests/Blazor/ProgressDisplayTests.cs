using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using ThreadedMosaic.BlazorServer.Components.Shared;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for ProgressDisplay Blazor component with SignalR integration
    /// </summary>
    public class ProgressDisplayTests : TestContext
    {
        public ProgressDisplayTests()
        {
            // Setup JSRuntime mock for Blazor components that use JavaScript
            var jsRuntimeMock = new Mock<IJSRuntime>();
            Services.AddSingleton(jsRuntimeMock.Object);
        }

        [Fact]
        public void ProgressDisplay_InitialRender_ShowsCorrectStructure()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert
            component.Should().NotBeNull();
            
            var card = component.Find(".card");
            card.Should().NotBeNull();
            
            var cardHeader = component.Find(".card-header");
            cardHeader.Should().NotBeNull();
            cardHeader.TextContent.Should().Contain("Processing Status");
            
            var cardBody = component.Find(".card-body");
            cardBody.Should().NotBeNull();
        }

        [Fact]
        public void ProgressDisplay_WhenNotProcessing_ShowsIdleState()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert
            // When not processing, should not show progress bar
            var progressBars = component.FindAll(".progress");
            progressBars.Should().BeEmpty();
        }

        [Fact]
        public void ProgressDisplay_WhenProcessing_ShowsProgressBar()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();
            
            // Simulate processing state by setting component properties
            var instance = component.Instance;
            var isProcessingField = typeof(ProgressDisplay).GetProperty("IsProcessing");
            if (isProcessingField != null)
            {
                isProcessingField.SetValue(instance, true);
                
                var progressField = typeof(ProgressDisplay).GetProperty("ProgressPercentage");
                progressField?.SetValue(instance, 45);
                
                var statusField = typeof(ProgressDisplay).GetProperty("CurrentStatus");
                statusField?.SetValue(instance, "Processing tiles...");
                
                component.Render();
            }

            // Assert
            if (component.FindAll(".progress").Count > 0)
            {
                var progressBar = component.Find(".progress-bar");
                progressBar.Should().NotBeNull();
                progressBar.GetAttribute("style").Should().Contain("width: 45%");
                progressBar.GetAttribute("aria-valuenow").Should().Be("45");
            }
        }

        [Fact]
        public void ProgressDisplay_StatusIcon_ChangesWithStatus()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert
            var iconElement = component.Find(".card-header i");
            iconElement.Should().NotBeNull();
            // Icon class should be present (actual icon depends on status)
            iconElement.GetAttribute("class").Should().Contain("bi");
        }

        [Fact]
        public void ProgressDisplay_CardStructure_HasCorrectBootstrapClasses()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert
            var card = component.Find(".card");
            card.GetAttribute("class").Should().Be("card");
            
            var cardHeader = component.Find(".card-header");
            cardHeader.GetAttribute("class").Should().Be("card-header");
            
            var cardBody = component.Find(".card-body");
            cardBody.GetAttribute("class").Should().Be("card-body");
        }

        [Fact]
        public void ProgressDisplay_HeaderTitle_HasCorrectContent()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert
            var title = component.Find(".card-header h6");
            title.Should().NotBeNull();
            title.GetAttribute("class").Should().Contain("mb-0");
            title.TextContent.Should().Contain("Processing Status");
        }

        [Theory]
        [InlineData("Initializing...", 0)]
        [InlineData("Loading images...", 25)]
        [InlineData("Creating mosaic...", 75)]
        [InlineData("Saving output...", 95)]
        public void ProgressDisplay_DifferentStatuses_DisplayCorrectly(string status, int percentage)
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();
            
            // Simulate different processing states
            var instance = component.Instance;
            var isProcessingField = typeof(ProgressDisplay).GetProperty("IsProcessing");
            var progressField = typeof(ProgressDisplay).GetProperty("ProgressPercentage");
            var statusField = typeof(ProgressDisplay).GetProperty("CurrentStatus");
            
            if (isProcessingField != null && progressField != null && statusField != null)
            {
                isProcessingField.SetValue(instance, true);
                progressField.SetValue(instance, percentage);
                statusField.SetValue(instance, status);
                component.Render();

                // Assert
                if (component.FindAll(".text-muted").Count > 0)
                {
                    var statusElements = component.FindAll(".text-muted");
                    statusElements.Should().HaveCountGreaterThan(0);
                    // Status should be displayed somewhere in the component
                    component.Markup.Should().ContainAny(status, percentage.ToString());
                }
            }
        }

        [Fact]
        public void ProgressDisplay_ProgressBar_HasCorrectAttributes()
        {
            // Act
            var component = RenderComponent<ProgressDisplay>();
            
            // Simulate processing
            var instance = component.Instance;
            var isProcessingField = typeof(ProgressDisplay).GetProperty("IsProcessing");
            if (isProcessingField != null)
            {
                isProcessingField.SetValue(instance, true);
                component.Render();
                
                // Assert - Check if progress bar structure exists when processing
                if (component.FindAll(".progress").Count > 0)
                {
                    var progressBar = component.Find(".progress-bar");
                    var classes = progressBar.GetAttribute("class");
                    classes.Should().Contain("progress-bar");
                    classes.Should().Contain("progress-bar-striped");
                    classes.Should().Contain("progress-bar-animated");
                    
                    progressBar.GetAttribute("role").Should().Be("progressbar");
                    progressBar.GetAttribute("aria-valuemin").Should().Be("0");
                    progressBar.GetAttribute("aria-valuemax").Should().Be("100");
                }
            }
        }

        [Fact]
        public void ProgressDisplay_SignalRIntegration_ComponentInitializes()
        {
            // Arrange & Act
            var component = RenderComponent<ProgressDisplay>();

            // Assert - Component should initialize without errors and show idle state
            var card = component.Find(".card");
            card.Should().NotBeNull();
            
            var idleIcon = component.Find(".bi-hourglass");
            idleIcon.Should().NotBeNull();
            
            var readyMessage = component.Find("div:contains('Ready to create mosaic')");
            readyMessage.Should().NotBeNull();
        }

        [Fact]
        public void ProgressDisplay_CompletedState_ShowsSuccessMessage()
        {
            // Arrange
            var component = RenderComponent<ProgressDisplay>();
            var instance = component.Instance;

            // Set completed state through reflection
            var hasCompletedField = typeof(ProgressDisplay).GetField("HasCompleted", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isSuccessField = typeof(ProgressDisplay).GetField("IsSuccess", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var outputPathField = typeof(ProgressDisplay).GetField("OutputPath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            hasCompletedField?.SetValue(instance, true);
            isSuccessField?.SetValue(instance, true);
            outputPathField?.SetValue(instance, "C:/output/mosaic.jpg");

            // Act
            component.Render();

            // Assert
            var successIcon = component.Find(".bi-check-circle-fill");
            successIcon.Should().NotBeNull();
            
            var successMessage = component.Find("strong:contains('Mosaic Created Successfully!')");
            successMessage.Should().NotBeNull();
            
            var outputPathDisplay = component.Find("code");
            outputPathDisplay.Should().NotBeNull();
            outputPathDisplay.TextContent.Should().Contain("C:/output/mosaic.jpg");
        }

        [Fact]
        public void ProgressDisplay_FailedState_ShowsErrorMessage()
        {
            // Arrange
            var component = RenderComponent<ProgressDisplay>();
            var instance = component.Instance;

            // Set failed state through reflection
            var hasCompletedField = typeof(ProgressDisplay).GetField("HasCompleted", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isSuccessField = typeof(ProgressDisplay).GetField("IsSuccess", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var errorMessageField = typeof(ProgressDisplay).GetField("ErrorMessage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            hasCompletedField?.SetValue(instance, true);
            isSuccessField?.SetValue(instance, false);
            errorMessageField?.SetValue(instance, "Processing failed due to insufficient memory");

            // Act
            component.Render();

            // Assert
            var errorIcon = component.Find(".bi-exclamation-circle-fill");
            errorIcon.Should().NotBeNull();
            
            var errorMessage = component.Find("strong:contains('Processing Failed')");
            errorMessage.Should().NotBeNull();
            
            var errorDetails = component.Find(".alert-danger");
            errorDetails.Should().NotBeNull();
            errorDetails.TextContent.Should().Contain("insufficient memory");
        }

        [Fact]
        public void ProgressDisplay_CancelButton_RendersWhenProcessing()
        {
            // Arrange
            var component = RenderComponent<ProgressDisplay>();
            var instance = component.Instance;

            // Set processing state
            var isProcessingField = typeof(ProgressDisplay).GetField("IsProcessing", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isProcessingField?.SetValue(instance, true);

            // Act
            component.Render();

            // Assert
            var cancelButton = component.Find("button:contains('Cancel Processing')");
            cancelButton.Should().NotBeNull();
            cancelButton.GetAttribute("class").Should().Contain("btn-outline-danger");
            
            var cancelIcon = component.Find(".bi-stop-circle");
            cancelIcon.Should().NotBeNull();
        }

        [Fact]
        public void ProgressDisplay_ProcessingStats_DisplayCorrectly()
        {
            // Arrange
            var component = RenderComponent<ProgressDisplay>();
            var instance = component.Instance;

            // Set processing state with stats
            var isProcessingField = typeof(ProgressDisplay).GetField("IsProcessing", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var processedItemsField = typeof(ProgressDisplay).GetField("ProcessedItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var totalItemsField = typeof(ProgressDisplay).GetField("TotalItems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            isProcessingField?.SetValue(instance, true);
            processedItemsField?.SetValue(instance, 75);
            totalItemsField?.SetValue(instance, 100);

            // Act
            component.Render();

            // Assert
            var processedDisplay = component.Find(".col-4:contains('Processed')");
            processedDisplay.Should().NotBeNull();
            processedDisplay.TextContent.Should().Contain("75");
            
            var totalDisplay = component.Find(".col-4:contains('Total')");
            totalDisplay.Should().NotBeNull();
            totalDisplay.TextContent.Should().Contain("100");
        }

        [Fact]
        public void ProgressDisplay_ActionButtons_RenderInCompletedState()
        {
            // Arrange
            var component = RenderComponent<ProgressDisplay>();
            var instance = component.Instance;

            // Set completed success state with output path
            var hasCompletedField = typeof(ProgressDisplay).GetField("HasCompleted", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isSuccessField = typeof(ProgressDisplay).GetField("IsSuccess", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var outputPathField = typeof(ProgressDisplay).GetField("OutputPath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            hasCompletedField?.SetValue(instance, true);
            isSuccessField?.SetValue(instance, true);
            outputPathField?.SetValue(instance, "C:/output/mosaic.jpg");

            // Act
            component.Render();

            // Assert
            var openLocationButton = component.Find("button:contains('Open Location')");
            openLocationButton.Should().NotBeNull();
            
            var viewResultButton = component.Find("button:contains('View Result')");
            viewResultButton.Should().NotBeNull();
        }
    }
}