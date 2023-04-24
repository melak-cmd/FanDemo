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
        public FanPanel()
        {
            this.Background = Brushes.Red;                    // Good for debugging
            this.Background = Brushes.Transparent;            // Make sure we get mouse events
            this.MouseEnter += new MouseEventHandler(FanPanel_MouseEnter);
            this.MouseLeave += new MouseEventHandler(FanPanel_MouseLeave);
        }

        private Size ourSize;
        private bool foundNewChildren = false;
        private double scaleFactor = 1;

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

        public bool IsWrapPanel
        {
            get { return (bool)GetValue(IsWrapPanelProperty); }
            set { SetValue(IsWrapPanelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsWrapPanel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsWrapPanelProperty =
            DependencyProperty.Register("IsWrapPanel", typeof(bool), typeof(FanPanel), new UIPropertyMetadata(false, IsWrapPanelChanged));

        public static void IsWrapPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Wrap Panel Changed");
            FanPanel me = (FanPanel)d;
            me.InvalidateArrange();
        }

        public int AnimationMilliseconds
        {
            get { return (int)GetValue(AnimationMillisecondsProperty); }
            set { SetValue(AnimationMillisecondsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for AnimationMilliseconds.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AnimationMillisecondsProperty =
            DependencyProperty.Register("AnimationMilliseconds", typeof(int), typeof(FanPanel), new UIPropertyMetadata(1250));

        protected override Size MeasureOverride(Size availableSize)
        {
            // Allow children as much room as they want - then scale them
            Size size = new Size(Double.PositiveInfinity, Double.PositiveInfinity);
            foreach (UIElement child in Children)
            {
                child.Measure(size);
            }

            // EID calls us with infinity, but framework doesn't like us to return infinity
            if (double.IsInfinity(availableSize.Height) || double.IsInfinity(availableSize.Width))
                return new Size(600, 600);
            else
                return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            RaiseEvent(new RoutedEventArgs(FanPanel.RefreshEvent, null));
            if (this.Children == null || this.Children.Count == 0)
                return finalSize;

            ourSize = finalSize;
            foundNewChildren = false;

            foreach (UIElement child in this.Children)
            {
                // If this is the first time we've seen this child, add our transforms
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

                // Don't allow our children any clicks in icon form
                child.IsHitTestVisible = IsWrapPanel;
                child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height));

                // Scale the children so they fit in our size
                double sf = (Math.Min(ourSize.Width, ourSize.Height) * 0.4) / Math.Max(child.DesiredSize.Width, child.DesiredSize.Height);
                scaleFactor = Math.Min(scaleFactor, sf);
            }

            AnimateAll();

            return finalSize;
        }

        void FanPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsWrapPanel)
            {
                System.Diagnostics.Debug.WriteLine("Mouse Enter");
                this.InvalidateArrange();
            }
        }

        void FanPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsWrapPanel)
            {
                System.Diagnostics.Debug.WriteLine("Mouse Leave");
                this.InvalidateArrange();
            }
        }

        private void AnimateAll()
        {
            System.Diagnostics.Debug.WriteLine("AnimateAll()");
            if (!IsWrapPanel)
            {
                if (!this.IsMouseOver)
                {
                    // Rotate all the children into a stack
                    double r = 0;
                    int sign = +1;
                    foreach (UIElement child in this.Children)
                    {
                        if (foundNewChildren)
                            child.SetValue(Panel.ZIndexProperty, 0);

                        AnimateTo(child, r, 0, 0, scaleFactor);
                        r += sign * 15;         // +-15 degree intervals
                        if (Math.Abs(r) > 90)
                        {
                            r = 0;
                            sign = -sign;
                        }
                    }
                }
                else
                {
                    // On mouse over explode out the children and don't rotate them
                    Random rand = new Random();
                    foreach (UIElement child in this.Children)
                    {
                        child.SetValue(Panel.ZIndexProperty, rand.Next(this.Children.Count));
                        double x = (rand.Next(16) - 8) * ourSize.Width / 32;
                        double y = (rand.Next(16) - 8) * ourSize.Height / 32;
                        AnimateTo(child, 0, x, y, scaleFactor);
                    }
                }
            }
            else
            {
                // Pretend to be a wrap panel
                double maxHeight = 0, x = 0, y = 0;
                foreach (UIElement child in this.Children)
                {
                    if (child.DesiredSize.Height > maxHeight)               // Row height
                        maxHeight = child.DesiredSize.Height;
                    if (x + child.DesiredSize.Width > this.ourSize.Width)
                    {
                        x = 0;
                        y += maxHeight;
                    }

                    if (y > this.ourSize.Height - maxHeight)
                        child.Visibility = Visibility.Hidden;
                    else
                        child.Visibility = Visibility.Visible;

                    AnimateTo(child, 0, x, y, 1);
                    x += child.DesiredSize.Width;
                }
            }
        }

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

        void anim_Completed(object sender, EventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FanPanel.AnimationCompletedEvent, e));
        }
    }
}
