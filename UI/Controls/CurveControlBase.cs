using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowWheel.Core;

namespace FlowWheel.UI.Controls
{
    using WpfPoint = System.Windows.Point;
    using WpfColor = System.Windows.Media.Color;
    using WpfBrush = System.Windows.Media.Brush;

    /// <summary>
    /// Shared base class for CurveEditor and CurvePreview, extracting common
    /// coordinate conversion, grid drawing, and theme adaptation logic.
    /// </summary>
    public abstract class CurveControlBase : System.Windows.Controls.Control
    {
        protected Canvas? _canvas;
        protected Path? _curvePath;
        protected bool _isLoaded = false;
        protected bool _themeChangeHandlerAdded = false;
        protected readonly List<UIElement> _canvasElements = new List<UIElement>();

        protected const double AxisMarginLeft = 28;
        protected const double AxisMarginBottom = 22;
        protected const double AxisMarginTop = 6;
        protected const double AxisMarginRight = 6;

        protected CurveControlBase()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        protected virtual void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            if (!_themeChangeHandlerAdded)
            {
                SystemParameters.StaticPropertyChanged += OnSystemPropertyChanged;
                _themeChangeHandlerAdded = true;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Redraw();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        protected virtual void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_themeChangeHandlerAdded)
            {
                SystemParameters.StaticPropertyChanged -= OnSystemPropertyChanged;
                _themeChangeHandlerAdded = false;
            }
        }

        private void OnSystemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                Redraw();
            }
        }

        protected virtual void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isLoaded) Redraw();
        }

        #region Coordinate helpers

        protected (double plotW, double plotH) GetPlotSize()
        {
            double cw = _canvas?.ActualWidth ?? 300;
            double ch = _canvas?.ActualHeight ?? 200;
            double pw = Math.Max(1, cw - AxisMarginLeft - AxisMarginRight);
            double ph = Math.Max(1, ch - AxisMarginTop - AxisMarginBottom);
            return (pw, ph);
        }

        protected (double cx, double cy) ToCanvas(double nx, double ny)
        {
            var (pw, ph) = GetPlotSize();
            return (AxisMarginLeft + nx * pw,
                    AxisMarginTop + (1.0 - ny) * ph);
        }

        protected (double nx, double ny) ToNormalised(double cx, double cy)
        {
            var (pw, ph) = GetPlotSize();
            double nx = (cx - AxisMarginLeft) / pw;
            double ny = 1.0 - (cy - AxisMarginTop) / ph;
            return (Math.Clamp(nx, 0, 1), Math.Clamp(ny, 0, 1));
        }

        #endregion

        #region Canvas element management

        protected void ClearCanvasElements()
        {
            if (_canvas == null) return;
            foreach (var el in _canvasElements)
                _canvas.Children.Remove(el);
            _canvasElements.Clear();
        }

        protected void AddCanvasElement(UIElement element)
        {
            _canvas?.Children.Add(element);
            _canvasElements.Add(element);
        }

        #endregion

        #region Grid rendering (shared)

        protected void DrawGrid()
        {
            if (_canvas == null) return;

            var (pw, ph) = GetPlotSize();
            var gridBrush = GetGridBrush();
            var axisBrush = GetAxisBrush();
            var labelBrush = GetLabelBrush();
            var diagBrush = GetDiagonalBrush();

            for (int i = 0; i <= 10; i++)
            {
                double x = AxisMarginLeft + i * pw / 10;
                var vline = new Line
                {
                    X1 = x, Y1 = AxisMarginTop,
                    X2 = x, Y2 = AxisMarginTop + ph,
                    Stroke = gridBrush, StrokeThickness = 1
                };
                AddCanvasElement(vline);

                if (i % 2 == 0)
                {
                    var tb = new TextBlock
                    {
                        Text = (i / 10.0).ToString("0.0"),
                        FontSize = 8,
                        Foreground = labelBrush
                    };
                    Canvas.SetLeft(tb, x - 8);
                    Canvas.SetTop(tb, AxisMarginTop + ph + 3);
                    AddCanvasElement(tb);
                }
            }

            for (int i = 0; i <= 10; i++)
            {
                double y = AxisMarginTop + i * ph / 10;
                var hline = new Line
                {
                    X1 = AxisMarginLeft, Y1 = y,
                    X2 = AxisMarginLeft + pw, Y2 = y,
                    Stroke = gridBrush, StrokeThickness = 1
                };
                AddCanvasElement(hline);

                if (i % 2 == 0)
                {
                    var tb = new TextBlock
                    {
                        Text = (1.0 - i / 10.0).ToString("0.0"),
                        FontSize = 8,
                        Foreground = labelBrush
                    };
                    Canvas.SetLeft(tb, 2);
                    Canvas.SetTop(tb, y - 6);
                    AddCanvasElement(tb);
                }
            }

            AddLine(AxisMarginLeft, AxisMarginTop + ph, AxisMarginLeft + pw, AxisMarginTop + ph, axisBrush, 2);
            AddLine(AxisMarginLeft, AxisMarginTop, AxisMarginLeft, AxisMarginTop + ph, axisBrush, 2);
            AddLine(AxisMarginLeft, AxisMarginTop + ph, AxisMarginLeft + pw, AxisMarginTop, diagBrush, 1,
                new DoubleCollection { 4, 2 });
        }

        protected void AddLine(double x1, double y1, double x2, double y2,
            WpfBrush stroke, double thickness, DoubleCollection? dash = null)
        {
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = thickness
            };
            if (dash != null) line.StrokeDashArray = dash;
            AddCanvasElement(line);
        }

        #endregion

        #region Curve path generation (shared)

        protected PathGeometry BuildCurveGeometry(Func<double, double> evaluateY, int segments = 120)
        {
            var geometry = new PathGeometry();
            var (sx, sy) = ToCanvas(0, evaluateY(0));
            var figure = new PathFigure { StartPoint = new WpfPoint(sx, sy) };

            for (int i = 1; i <= segments; i++)
            {
                double t = (double)i / segments;
                double y = evaluateY(t);
                var (cx, cy) = ToCanvas(t, y);
                figure.Segments.Add(new LineSegment(new WpfPoint(cx, cy), true));
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        #endregion

        #region Theme brushes (shared)

        protected WpfColor GetAccentColor()
        {
            if (TryFindResource("Brush.Curve.Line") is SolidColorBrush brush)
                return brush.Color;
            return WpfColor.FromRgb(0, 120, 212);
        }

        protected WpfBrush GetGridBrush()
        {
            if (TryFindResource("CurveEditorGridBrush") is WpfBrush brush)
                return brush;
            if (TryFindResource("Brush.Curve.Grid") is WpfBrush brush2)
                return brush2;
            return new SolidColorBrush(WpfColor.FromArgb(60, 180, 180, 180));
        }

        protected WpfBrush GetAxisBrush()
        {
            if (TryFindResource("CurveEditorAxisBrush") is WpfBrush brush)
                return brush;
            if (TryFindResource("Brush.Curve.Axis") is WpfBrush brush2)
                return brush2;
            return new SolidColorBrush(WpfColor.FromRgb(100, 100, 100));
        }

        protected WpfBrush GetLabelBrush()
        {
            if (TryFindResource("CurveEditorLabelBrush") is WpfBrush brush)
                return brush;
            if (TryFindResource("Brush.Curve.Label") is WpfBrush brush2)
                return brush2;
            return new SolidColorBrush(WpfColor.FromRgb(80, 80, 80));
        }

        protected WpfBrush GetDiagonalBrush()
        {
            if (TryFindResource("Brush.Curve.Diagonal") is WpfBrush brush)
                return brush;
            return new SolidColorBrush(WpfColor.FromArgb(150, 150, 150, 150));
        }

        protected WpfColor GetPointFillColor()
        {
            if (TryFindResource("Brush.Curve.PointFill") is SolidColorBrush brush)
                return brush.Color;
            return Colors.White;
        }

        #endregion

        protected abstract void Redraw();
    }
}
