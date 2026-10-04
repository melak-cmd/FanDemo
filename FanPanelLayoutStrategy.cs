using System;
using System.Collections.Generic;
using System.Windows;

namespace FanDemo
{
    internal interface IFanPanelLayoutStrategy
    {
        IReadOnlyList<FanPanelChildLayout> Calculate(
            IReadOnlyList<Size> childSizes,
            Size panelSize,
            double scaleFactor,
            bool isMouseOver,
            bool foundNewChildren);
    }

    internal readonly struct FanPanelChildLayout
    {
        public FanPanelChildLayout(
            double rotation,
            double x,
            double y,
            double scale,
            int? zIndex = null,
            Visibility? visibility = null)
        {
            Rotation = rotation;
            X = x;
            Y = y;
            Scale = scale;
            ZIndex = zIndex;
            Visibility = visibility;
        }

        public double Rotation { get; }
        public double X { get; }
        public double Y { get; }
        public double Scale { get; }
        public int? ZIndex { get; }
        public Visibility? Visibility { get; }
    }

    internal sealed class FanPanelFanLayoutStrategy : IFanPanelLayoutStrategy
    {
        public IReadOnlyList<FanPanelChildLayout> Calculate(
            IReadOnlyList<Size> childSizes,
            Size panelSize,
            double scaleFactor,
            bool isMouseOver,
            bool foundNewChildren)
        {
            var layouts = new FanPanelChildLayout[childSizes.Count];
            if (!isMouseOver)
            {
                double rotation = 0;
                int sign = +1;
                for (int i = 0; i < childSizes.Count; i++)
                {
                    layouts[i] = new FanPanelChildLayout(
                        rotation,
                        0,
                        0,
                        scaleFactor,
                        foundNewChildren ? 0 : (int?)null);

                    rotation += sign * 15;
                    if (Math.Abs(rotation) > 90)
                    {
                        rotation = 0;
                        sign = -sign;
                    }
                }

                return layouts;
            }

            var random = new Random();
            for (int i = 0; i < childSizes.Count; i++)
            {
                var x = (random.Next(16) - 8) * panelSize.Width / 32;
                var y = (random.Next(16) - 8) * panelSize.Height / 32;
                layouts[i] = new FanPanelChildLayout(
                    0,
                    x,
                    y,
                    scaleFactor,
                    random.Next(childSizes.Count));
            }

            return layouts;
        }
    }

    internal sealed class FanPanelWrapLayoutStrategy : IFanPanelLayoutStrategy
    {
        public IReadOnlyList<FanPanelChildLayout> Calculate(
            IReadOnlyList<Size> childSizes,
            Size panelSize,
            double scaleFactor,
            bool isMouseOver,
            bool foundNewChildren)
        {
            var layouts = new FanPanelChildLayout[childSizes.Count];
            double maxHeight = 0;
            double x = 0;
            double y = 0;

            for (int i = 0; i < childSizes.Count; i++)
            {
                var childSize = childSizes[i];
                if (childSize.Height > maxHeight)
                    maxHeight = childSize.Height;

                if (x + childSize.Width > panelSize.Width)
                {
                    x = 0;
                    y += maxHeight;
                }

                var visibility = y > panelSize.Height - maxHeight
                    ? Visibility.Hidden
                    : Visibility.Visible;
                layouts[i] = new FanPanelChildLayout(0, x, y, 1, visibility: visibility);
                x += childSize.Width;
            }

            return layouts;
        }
    }
}
