using FanDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FanDemo.Tests;

[TestClass]
public class FanPanelTests
{
    [STATestMethod]
    public void Measure_WithFiniteAvailableSize_UsesAvailableSize()
    {
        var panel = new FanPanel();
        var availableSize = new Size(320, 240);

        panel.Measure(availableSize);

        Assert.AreEqual(availableSize, panel.DesiredSize);
    }

    [STATestMethod]
    public void Measure_WithUnboundedAvailableSize_UsesFallbackSize()
    {
        var panel = new FanPanel();

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Assert.AreEqual(new Size(600, 600), panel.DesiredSize);
    }

    [STATestMethod]
    public void Arrange_InFanMode_InitializesTransformAndDisablesHitTesting()
    {
        var panel = new FanPanel { AnimationMilliseconds = 0 };
        var child = new Border { Width = 80, Height = 40 };
        panel.Children.Add(child);

        panel.Measure(new Size(200, 200));
        panel.Arrange(new Rect(0, 0, 200, 200));

        Assert.IsFalse(child.IsHitTestVisible);
        Assert.AreEqual(new Point(0.5, 0.5), child.RenderTransformOrigin);
        Assert.IsInstanceOfType(child.RenderTransform, typeof(TransformGroup));
        Assert.AreEqual(3, ((TransformGroup)child.RenderTransform).Children.Count);
    }

    [STATestMethod]
    public void Arrange_InWrapMode_EnablesHitTestingAndHidesOverflowChildren()
    {
        var panel = new FanPanel { AnimationMilliseconds = 0, IsWrapPanel = true };
        var firstChild = new Border { Width = 80, Height = 40 };
        var overflowChild = new Border { Width = 80, Height = 40 };
        panel.Children.Add(firstChild);
        panel.Children.Add(overflowChild);

        panel.Measure(new Size(100, 40));
        panel.Arrange(new Rect(0, 0, 100, 40));

        Assert.IsTrue(firstChild.IsHitTestVisible);
        Assert.IsTrue(overflowChild.IsHitTestVisible);
        Assert.AreEqual(Visibility.Visible, firstChild.Visibility);
        Assert.AreEqual(Visibility.Hidden, overflowChild.Visibility);
    }

    [STATestMethod]
    public void Arrange_WhenIsWrapPanelChanges_UsesSelectedLayoutStrategy()
    {
        var panel = new FanPanel { AnimationMilliseconds = 0 };
        var firstChild = new Border { Width = 80, Height = 40 };
        var overflowChild = new Border { Width = 80, Height = 40 };
        panel.Children.Add(firstChild);
        panel.Children.Add(overflowChild);
        var panelRect = new Rect(0, 0, 100, 40);

        panel.Measure(panelRect.Size);
        panel.Arrange(panelRect);
        Assert.IsFalse(firstChild.IsHitTestVisible);
        Assert.AreEqual(Visibility.Visible, overflowChild.Visibility);

        panel.IsWrapPanel = true;
        panel.Arrange(panelRect);

        Assert.IsTrue(firstChild.IsHitTestVisible);
        Assert.IsTrue(overflowChild.IsHitTestVisible);
        Assert.AreEqual(Visibility.Hidden, overflowChild.Visibility);

        panel.IsWrapPanel = false;
        panel.Arrange(panelRect);

        Assert.IsFalse(firstChild.IsHitTestVisible);
        Assert.IsFalse(overflowChild.IsHitTestVisible);
    }
}
