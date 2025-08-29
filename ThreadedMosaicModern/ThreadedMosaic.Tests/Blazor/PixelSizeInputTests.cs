using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ThreadedMosaic.BlazorServer.Components.Shared;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for PixelSizeInput Blazor component
    /// </summary>
    public class PixelSizeInputTests : TestContext
    {
        [Fact]
        public void PixelSizeInput_RendersCorrectly()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            component.Should().NotBeNull();
            
            var label = component.Find("label");
            label.Should().NotBeNull();
            label.TextContent.Should().Contain("Tile Size");
            
            var rangeInput = component.Find("input[type=range]");
            rangeInput.Should().NotBeNull();
            rangeInput.GetAttribute("class").Should().Contain("form-range");
            
            var numberInput = component.Find("input[type=number]");
            numberInput.Should().NotBeNull();
            numberInput.GetAttribute("class").Should().Contain("form-control");
        }

        [Fact]
        public void PixelSizeInput_DefaultValue_IsCorrect()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var input = component.Find("input[type=range]");
            var defaultValue = input.GetAttribute("value");
            
            // Default should be reasonable (e.g., 16)
            int.TryParse(defaultValue, out int value).Should().BeTrue();
            value.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PixelSizeInput_HasCorrectRangeAttributes()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var input = component.Find("input[type=range]");
            
            var min = input.GetAttribute("min");
            var max = input.GetAttribute("max");
            var step = input.GetAttribute("step");
            
            int.TryParse(min, out int minValue).Should().BeTrue();
            int.TryParse(max, out int maxValue).Should().BeTrue();
            
            minValue.Should().BeGreaterThan(0);
            maxValue.Should().BeGreaterThan(minValue);
            
            // Step should be reasonable
            if (!string.IsNullOrEmpty(step))
            {
                int.TryParse(step, out int stepValue).Should().BeTrue();
                stepValue.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void PixelSizeInput_DisplaysCurrentValue()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            // Should display the current value somewhere (e.g., in a span or as part of label)
            var valueDisplay = component.FindAll("span, .form-text, .badge");
            
            if (valueDisplay.Count > 0)
            {
                var hasNumericValue = valueDisplay.Any(element => 
                {
                    var text = element.TextContent;
                    return int.TryParse(text.Trim().Replace("px", ""), out int _);
                });
                
                // If there's a value display, it should show a numeric value
                if (valueDisplay.Count > 0)
                {
                    component.Markup.Should().MatchRegex(@"\d+");
                }
            }
        }

        [Fact]
        public void PixelSizeInput_PixelSizeParameter_SetsInputValue()
        {
            // Arrange
            int expectedValue = 32;

            // Act
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, expectedValue));

            // Assert
            var input = component.Find("input[type=range]");
            input.GetAttribute("value").Should().Be(expectedValue.ToString());
        }

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public void PixelSizeInput_DifferentValues_RenderCorrectly(int pixelSize)
        {
            // Act
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, pixelSize));

            // Assert
            var input = component.Find("input[type=range]");
            input.GetAttribute("value").Should().Be(pixelSize.ToString());
            
            // The component should display the value somewhere
            component.Markup.Should().Contain(pixelSize.ToString());
        }

        [Fact]
        public void PixelSizeInput_HasBootstrapFormClasses()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var container = component.Find(".mb-3");
            container.Should().NotBeNull();
            
            var label = component.Find("label");
            label.GetAttribute("class").Should().Contain("form-label");
            
            var input = component.Find("input[type=range]");
            input.GetAttribute("class").Should().Contain("form-range");
        }

        [Fact]
        public void PixelSizeInput_RangeInput_HasCorrectValueBinding()
        {
            // Arrange
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, 24));

            // Act
            var input = component.Find("input[type=range]");

            // Assert
            input.GetAttribute("value").Should().Be("24");
        }

        [Fact]
        public void PixelSizeInput_Label_HasForAttribute()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var label = component.Find("label");
            var input = component.Find("input[type=range]");
            
            var forAttribute = label.GetAttribute("for");
            var inputId = input.GetAttribute("id");
            
            if (!string.IsNullOrEmpty(forAttribute) && !string.IsNullOrEmpty(inputId))
            {
                forAttribute.Should().Be(inputId);
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        public void PixelSizeInput_PresetButtons_SetCorrectValues(int expectedValue)
        {
            // Arrange
            int receivedValue = 0;
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSizeChanged, (int value) => receivedValue = value));

            // Act
            var button = component.Find($"button:contains('{expectedValue}px')");
            button.Click();

            // Assert
            receivedValue.Should().Be(expectedValue);
            
            // Verify button is now active
            button.GetAttribute("class").Should().Contain("btn-primary");
        }

        [Fact]
        public void PixelSizeInput_PresetButtons_UpdateStyling()
        {
            // Arrange
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, 16));

            // Assert - 16px button should be active
            var activeButton = component.Find("button:contains('16px')");
            activeButton.GetAttribute("class").Should().Contain("btn-primary");
            
            var inactiveButton = component.Find("button:contains('32px')");
            inactiveButton.GetAttribute("class").Should().Contain("btn-outline-secondary");
        }

        [Fact]
        public void PixelSizeInput_ProcessingTimeEstimate_DisplaysCorrectly()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, 16));

            // Assert
            var helpText = component.Find(".form-text");
            helpText.TextContent.Should().Contain("16 px tiles");
            helpText.TextContent.Should().Contain("Slow processing time");
        }

        [Theory]
        [InlineData(8, "Very slow")]
        [InlineData(16, "Slow")]
        [InlineData(24, "Moderate")]
        [InlineData(32, "Fast")]
        [InlineData(48, "Very fast")]
        [InlineData(96, "Extremely fast")]
        public void PixelSizeInput_ProcessingTimeEstimate_VariesBySize(int pixelSize, string expectedEstimate)
        {
            // Act
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, pixelSize));

            // Assert
            var helpText = component.Find(".form-text");
            helpText.TextContent.Should().Contain(expectedEstimate);
        }

        [Fact]
        public void PixelSizeInput_RangeAndNumberInputs_AreSynchronized()
        {
            // Arrange
            var component = RenderComponent<PixelSizeInput>(parameters => parameters
                .Add(p => p.PixelSize, 32));

            // Assert - Both inputs should show the same value
            var rangeInput = component.Find("input[type=range]");
            var numberInput = component.Find("input[type=number]");
            
            rangeInput.GetAttribute("value").Should().Be("32");
            numberInput.GetAttribute("value").Should().Be("32");
        }

        [Fact]
        public void PixelSizeInput_HelpText_ShowsProcessingTradeoff()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var helpText = component.Find(".form-text");
            helpText.TextContent.Should().Contain("Smaller tiles = More detail, longer processing");
            helpText.TextContent.Should().Contain("Larger tiles = Faster processing, less detail");
        }

        [Fact]
        public void PixelSizeInput_HasCorrectInputConstraints()
        {
            // Act
            var component = RenderComponent<PixelSizeInput>();

            // Assert
            var rangeInput = component.Find("input[type=range]");
            rangeInput.GetAttribute("min").Should().Be("4");
            rangeInput.GetAttribute("max").Should().Be("128");
            rangeInput.GetAttribute("step").Should().Be("4");

            var numberInput = component.Find("input[type=number]");
            numberInput.GetAttribute("min").Should().Be("4");
            numberInput.GetAttribute("max").Should().Be("128");
            numberInput.GetAttribute("step").Should().Be("4");
        }
    }
}