/*
 * FanPanel Class
 * 
 * This class is a custom WPF (Windows Presentation Foundation) panel that arranges its child elements in a fan-like manner
 * and provides animations and interaction based on mouse events. It inherits from the System.Windows.Controls.Panel class.
 * 
 * Author: [Your Name]
 * Date: [Date]
 */

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FanDemo
{
    public partial class FanPanel : System.Windows.Controls.Panel
    {
        private static readonly IFanPanelLayoutStrategy FanLayoutStrategy = new FanPanelFanLayoutStrategy();
        private static readonly IFanPanelLayoutStrategy WrapLayoutStrategy = new FanPanelWrapLayoutStrategy();

        // Constructor
        public FanPanel()
        {
            // Set up initial properties and event handlers
            this.Background = Brushes.Transparent; // Make sure we get mouse events
            this.MouseEnter += new MouseEventHandler(FanPanel_MouseEnter);
            this.MouseLeave += new MouseEventHandler(FanPanel_MouseLeave);
        }

        // Private Fields
        private Size ourSize;
        private bool foundNewChildren = false;
        private double scaleFactor = 1;

        // Custom Routed Events
        public static readonly RoutedEvent RefreshEvent = EventManager.RegisterRoutedEvent("Refresh", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FanPanel));
        public event RoutedEventHandler Refresh
        {
            add { AddHandler(RefreshEvent, value); }
            remove { RemoveHandler(RefreshEvent, value); }
        }

        public static readonly RoutedEvent AnimationCompletedEvent = EventManager.RegisterRoutedEvent("AnimationCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FanPanel));
        public event RoutedEventHandler AnimationCompleted
        {
            add { AddHandler(AnimationCompletedEvent, value); }
            remove { RemoveHandler(AnimationCompletedEvent, value); }
        }

        // Dependency Property: IsWrapPanel
        public bool IsWrapPanel
        {
            get { return (bool)GetValue(IsWrapPanelProperty); }
            set { SetValue(IsWrapPanelProperty, value); }
        }

        public static readonly DependencyProperty IsWrapPanelProperty =
            DependencyProperty.Register("IsWrapPanel", typeof(bool), typeof(FanPanel), new UIPropertyMetadata(false, IsWrapPanelChanged));

        private static void IsWrapPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Called when the IsWrapPanel property changes, triggers an arrange invalidation
            System.Diagnostics.Debug.WriteLine("Wrap Panel Changed");
            FanPanel me = (FanPanel)d;
            me.InvalidateArrange();
        }

        // Dependency Property: AnimationMilliseconds
        public int AnimationMilliseconds
        {
            get { return (int)GetValue(AnimationMillisecondsProperty); }
            set { SetValue(AnimationMillisecondsProperty, value); }
        }

        public static readonly DependencyProperty AnimationMillisecondsProperty =
            DependencyProperty.Register("AnimationMilliseconds", typeof(int), typeof(FanPanel), new UIPropertyMetadata(1250));

        // MeasureOverride: Measures the desired size of the panel's children
        protected override Size MeasureOverride(Size availableSize)
        {
            // Measure children with infinite space and set a fallback size
            Size size = new Size(Double.PositiveInfinity, Double.PositiveInfinity);
            foreach (UIElement child in Children)
            {
                child.Measure(size);
            }

            // Handle infinity case (EID calls)
            if (double.IsInfinity(availableSize.Height) || double.IsInfinity(availableSize.Width))
                return new Size(600, 600);
            else
                return availableSize;
        }

        // ArrangeOverride: Arranges the panel's children in a fan-like layout
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Raise the Refresh event
            RaiseEvent(new RoutedEventArgs(FanPanel.RefreshEvent, null));
            if (this.Children == null || this.Children.Count == 0)
                return finalSize;

            // Initialization
            ourSize = finalSize;
            foundNewChildren = false;

            foreach (UIElement child in this.Children)
            {
                // Apply transforms if not done before
                if (child.RenderTransform as TransformGroup == null)
                {
                    foundNewChildren = true;
                    child.RenderTransformOrigin = new Point(0.5, 0.5);
                    TransformGroup group = new TransformGroup();
                    child.RenderTransform = group;
                    group.Children.Add(new ScaleTransform());
                    group.Children.Add(new TranslateTransform());
                    group.Children.Add(new RotateTransform());
                }

                // Set hit test visibility and arrange children
                child.IsHitTestVisible = IsWrapPanel;
                child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height));

                // Calculate scaling factor
                double sf = (Math.Min(ourSize.Width, ourSize.Height) * 0.4) / Math.Max(child.DesiredSize.Width, child.DesiredSize.Height);
                scaleFactor = Math.Min(scaleFactor, sf);
            }

            // Perform animations
            AnimateAll();

            return finalSize;
        }

        // MouseEnter Event Handler
        void FanPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsWrapPanel)
            {
                System.Diagnostics.Debug.WriteLine("Mouse Enter");
                this.InvalidateArrange();
            }
        }

        // MouseLeave Event Handler
        void FanPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsWrapPanel)
            {
                System.Diagnostics.Debug.WriteLine("Mouse Leave");
                this.InvalidateArrange();
            }
        }

        // Animates all children
        private void AnimateAll()
        {
            System.Diagnostics.Debug.WriteLine("AnimateAll()");
            var childSizes = new Size[Children.Count];
            for (int i = 0; i < Children.Count; i++)
            {
                childSizes[i] = Children[i].DesiredSize;
            }

            var strategy = IsWrapPanel ? WrapLayoutStrategy : FanLayoutStrategy;
            var layouts = strategy.Calculate(childSizes, ourSize, scaleFactor, IsMouseOver, foundNewChildren);

            for (int i = 0; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                FanPanelChildLayout layout = layouts[i];
                if (layout.ZIndex.HasValue)
                    child.SetValue(Panel.ZIndexProperty, layout.ZIndex.Value);
                if (layout.Visibility.HasValue)
                    child.Visibility = layout.Visibility.Value;

                AnimateTo(child, layout.Rotation, layout.X, layout.Y, layout.Scale);
            }
        }

        // Animates an individual child's properties
        private void AnimateTo(UIElement child, double r, double x, double y, double s)
        {
            TransformGroup group = (TransformGroup)child.RenderTransform;
            ScaleTransform scale = (ScaleTransform)group.Children[0];
            TranslateTransform trans = (TranslateTransform)group.Children[1];
            RotateTransform rot = (RotateTransform)group.Children[2];

            rot.BeginAnimation(RotateTransform.AngleProperty, MakeAnimation(r, anim_Completed));
            trans.BeginAnimation(TranslateTransform.XProperty, MakeAnimation(x));
            trans.BeginAnimation(TranslateTransform.YProperty, MakeAnimation(y));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, MakeAnimation(s));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, MakeAnimation(s));
        }

        // Creates a DoubleAnimation with specified target value and optional completed event
        private DoubleAnimation MakeAnimation(double to)
        {
            return MakeAnimation(to, null);
        }

        private DoubleAnimation MakeAnimation(double to, EventHandler endEvent)
        {
            DoubleAnimation anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(AnimationMilliseconds));
            anim.AccelerationRatio = 0.2;
            anim.DecelerationRatio = 0.7;
            if (endEvent != null)
                anim.Completed += endEvent;
            return anim;
        }

        // Animation Completed Event Handler
        void anim_Completed(object sender, EventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FanPanel.AnimationCompletedEvent, e));
        }
    }
}
