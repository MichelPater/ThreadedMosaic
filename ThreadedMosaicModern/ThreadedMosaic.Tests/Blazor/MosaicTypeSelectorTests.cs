using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ThreadedMosaic.BlazorServer.Components.Shared;
using static ThreadedMosaic.BlazorServer.Components.Shared.MosaicTypeSelector;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for MosaicTypeSelector Blazor component
    /// </summary>
    public class MosaicTypeSelectorTests : TestContext
    {
        [Fact]
        public void MosaicTypeSelector_RendersCorrectly()
        {
            // Act
            var component = RenderComponent<MosaicTypeSelector>();

            // Assert
            component.Should().NotBeNull();
            component.Find("label").TextContent.Should().Contain("Mosaic Type");
            
            // Should have three radio buttons
            var radioButtons = component.FindAll("input[type='radio']");
            radioButtons.Count.Should().Be(3);
            
            // Should have Color, Hue, and Photo options
            var labels = component.FindAll("label.form-check-label");
            labels.Should().HaveCount(3);
            
            var labelTexts = labels.Select(l => l.TextContent).ToList();
            labelTexts.Should().Contain(l => l.Contains("Color Mosaic"));
            labelTexts.Should().Contain(l => l.Contains("Hue Mosaic"));
            labelTexts.Should().Contain(l => l.Contains("Photo Mosaic"));
        }

        [Fact]
        public void MosaicTypeSelector_DefaultsToColorType()
        {
            // Act
            var component = RenderComponent<MosaicTypeSelector>();

            // Assert
            var colorRadio = component.Find("#colorType");
            colorRadio.HasAttribute("checked").Should().BeTrue();
            
            var hueRadio = component.Find("#hueType");
            hueRadio.HasAttribute("checked").Should().BeFalse();
            
            var photoRadio = component.Find("#photoType");
            photoRadio.HasAttribute("checked").Should().BeFalse();
        }

        [Fact]
        public void MosaicTypeSelector_CanSelectHueType()
        {
            // Arrange
            var component = RenderComponent<MosaicTypeSelector>();
            var hueRadio = component.Find("#hueType");

            // Act
            hueRadio.Change(true);

            // Assert
            var updatedHueRadio = component.Find("#hueType");
            updatedHueRadio.HasAttribute("checked").Should().BeTrue();
            
            // Should show hue description
            var alertDiv = component.Find(".alert");
            alertDiv.TextContent.Should().Contain("Hue Mosaic:");
            alertDiv.TextContent.Should().Contain("matching hue values");
        }

        [Fact]
        public void MosaicTypeSelector_CanSelectPhotoType()
        {
            // Arrange
            var component = RenderComponent<MosaicTypeSelector>();
            var photoRadio = component.Find("#photoType");

            // Act
            photoRadio.Change(true);

            // Assert
            var updatedPhotoRadio = component.Find("#photoType");
            updatedPhotoRadio.HasAttribute("checked").Should().BeTrue();
            
            // Should show photo description
            var alertDiv = component.Find(".alert");
            alertDiv.TextContent.Should().Contain("Photo Mosaic:");
            alertDiv.TextContent.Should().Contain("actual tiles");
        }

        [Fact]
        public void MosaicTypeSelector_ShowsCorrectDescriptionForSelectedType()
        {
            // Arrange & Act
            var component = RenderComponent<MosaicTypeSelector>();

            // Assert - Default Color type should show color description
            var alertDiv = component.Find(".alert");
            alertDiv.TextContent.Should().Contain("Color Mosaic:");
            alertDiv.TextContent.Should().Contain("directly matching");
        }

        [Fact]
        public void MosaicTypeSelector_AppliesBorderToSelectedCard()
        {
            // Arrange
            var component = RenderComponent<MosaicTypeSelector>();

            // Assert - Should be able to find cards with proper CSS classes
            var cards = component.FindAll(".card");
            cards.Should().HaveCount(3);
            
            // At least one card should have border-primary class by default (color type selected)
            var cardWithBorder = cards.FirstOrDefault(c => c.GetAttribute("class")?.Contains("border-primary") == true);
            cardWithBorder.Should().NotBeNull();
        }

        [Fact]
        public void MosaicTypeSelector_WithInitialHueType_RendersCorrectly()
        {
            // Arrange & Act
            var component = RenderComponent<MosaicTypeSelector>(parameters => parameters
                .Add(p => p.SelectedType, MosaicType.Hue));

            // Assert
            var hueRadio = component.Find("#hueType");
            hueRadio.HasAttribute("checked").Should().BeTrue();
            
            var colorRadio = component.Find("#colorType");
            colorRadio.HasAttribute("checked").Should().BeFalse();
            
            var photoRadio = component.Find("#photoType");
            photoRadio.HasAttribute("checked").Should().BeFalse();
            
            // Should show hue description
            var alertDiv = component.Find(".alert");
            alertDiv.TextContent.Should().Contain("Hue Mosaic:");
        }

        [Fact]
        public void MosaicTypeSelector_TypeSelectionTriggersCallback()
        {
            // Arrange
            MosaicType? callbackResult = null;
            var component = RenderComponent<MosaicTypeSelector>(parameters => parameters
                .Add(p => p.SelectedTypeChanged, (MosaicType type) => callbackResult = type));

            // Act
            var hueRadio = component.Find("#hueType");
            hueRadio.Change(true);

            // Assert
            callbackResult.Should().Be(MosaicType.Hue);
        }
    }
}