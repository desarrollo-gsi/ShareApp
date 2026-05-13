using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Animation;

namespace AvaloniaShareApp.Infrastructure.Ui.Controls
{
    public class ImageDragEventArgs : EventArgs
    {
        public double DeltaX { get; }
        public double DeltaY { get; }
        public Point PositionInContainer { get; }

        public ImageDragEventArgs(double dx, double dy, Point pos)
        {
            DeltaX = dx;
            DeltaY = dy;
            PositionInContainer = pos;
        }
    }

    public enum ResizeOrigin
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public class SelectableImageControl : Grid
    {
        private Image _image;
        private Border _selectionAdorner;
        private Grid _resizeHandlesGrid;
        private bool _isSelected;

        public event EventHandler<EventArgs>? Selected;
        public string FilePath { get; set; } = "";
        
        // Drag Events
        public event EventHandler<EventArgs>? ImageDragStarted;
        public event EventHandler<ImageDragEventArgs>? ImageDragged;
        public event EventHandler<EventArgs>? ImageDragEnded;

        // Expose data for PDF Export
        public Bitmap SourceBitmap => (Bitmap)_image.Source!;
        public double ScaleX => _scaleTransform.ScaleX;
        public double ScaleY => _scaleTransform.ScaleY;
        public double Rotation
        {
            get => _rotateTransform.Angle;
            set => _rotateTransform.Angle = value;
        }
        public double CropLeft => _cropLeft;
        public double CropTop => _cropTop;
        public double CropRight => _cropRight;
        public double CropBottom => _cropBottom;
        public double BaseWidth => _baseWidth;
        public double BaseHeight => _baseHeight;

        // Transforms
        private ScaleTransform _scaleTransform;
        private RotateTransform _rotateTransform;
        private TransformGroup _transformGroup;

        // Resizing State
        private bool _isResizing;
        private ResizeOrigin _activeResizeOrigin;
        private Point _resizeStartPointGlobal;
        
        private double _originalWidth;
        private double _originalHeight;
        private double _originalCanvasLeft;
        private double _originalCanvasTop;

        // Dragging State
        private bool _isDraggingImage;
        private Point _imageDragStartPoint;

        // Crop State
        private bool _isCropMode;
        private double _baseWidth;
        private double _baseHeight;
        private double _cropLeft = 0;
        private double _cropTop = 0;
        private double _cropRight = 0;
        private double _cropBottom = 0;

        private double _originalCropLeft;
        private double _originalCropTop;
        private double _originalCropRight;
        private double _originalCropBottom;

        public SelectableImageControl(Bitmap bitmap)
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Background = null; 
            ClipToBounds = true; // Essential for cropping
            
            this.Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(0.2) }
            };

            _scaleTransform = new ScaleTransform(1, 1);
            _rotateTransform = new RotateTransform(0);
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_rotateTransform);

            _image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                RenderTransform = _transformGroup,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            _baseWidth = Math.Min(bitmap.Size.Width, 600);
            _baseHeight = (_baseWidth / bitmap.Size.Width) * bitmap.Size.Height;
            
            _image.Width = _baseWidth;
            _image.Height = _baseHeight;
            
            this.Width = _baseWidth;
            this.Height = _baseHeight;

            _selectionAdorner = new Border
            {
                BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(2),
                IsVisible = false,
                ZIndex = 10,
                Background = Brushes.Transparent // Catch clicks to drag body
            };

            _resizeHandlesGrid = new Grid();

            AddResizeHandle(HorizontalAlignment.Left, VerticalAlignment.Top, new Cursor(StandardCursorType.TopLeftCorner), ResizeOrigin.TopLeft, -6, -6);
            AddResizeHandle(HorizontalAlignment.Right, VerticalAlignment.Top, new Cursor(StandardCursorType.TopRightCorner), ResizeOrigin.TopRight, -6, -6);
            AddResizeHandle(HorizontalAlignment.Left, VerticalAlignment.Bottom, new Cursor(StandardCursorType.BottomLeftCorner), ResizeOrigin.BottomLeft, -6, -6);
            AddResizeHandle(HorizontalAlignment.Right, VerticalAlignment.Bottom, new Cursor(StandardCursorType.BottomRightCorner), ResizeOrigin.BottomRight, -6, -6);

            _selectionAdorner.Child = _resizeHandlesGrid;

            Children.Add(_image);
            Children.Add(_selectionAdorner);

            PointerPressed += OnControlPointerPressed;
            PointerMoved += OnControlPointerMoved;
            PointerReleased += OnControlPointerReleased;
        }

        private void AddResizeHandle(HorizontalAlignment hAlign, VerticalAlignment vAlign, Cursor cursor, ResizeOrigin origin, int hMargin, int vMargin)
        {
            var leftMargin = hAlign == HorizontalAlignment.Left ? hMargin : 0;
            var rightMargin = hAlign == HorizontalAlignment.Right ? hMargin : 0;
            var topMargin = vAlign == VerticalAlignment.Top ? vMargin : 0;
            var bottomMargin = vAlign == VerticalAlignment.Bottom ? vMargin : 0;

            var handle = new Border
            {
                Width = 12,
                Height = 12,
                Background = Brushes.White,
                BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = new Thickness(leftMargin, topMargin, rightMargin, bottomMargin),
                Cursor = cursor,
                ZIndex = 11,
                Tag = origin
            };

            handle.PointerPressed += OnResizePointerPressed;
            handle.PointerMoved += OnResizePointerMoved;
            handle.PointerReleased += OnResizePointerReleased;

            _resizeHandlesGrid.Children.Add(handle);
        }

        public void ToggleCropMode()
        {
            _isCropMode = !_isCropMode;
            
            var brush = _isCropMode ? Brushes.Black : Brushes.Blue;
            var handleBg = _isCropMode ? Brushes.Black : Brushes.White;
            _selectionAdorner.BorderBrush = brush;

            foreach (var child in _resizeHandlesGrid.Children)
            {
                if (child is Border b)
                {
                    b.BorderBrush = brush;
                    b.Background = handleBg;
                    if (_isCropMode)
                    {
                        // Crop handles usually indicate sliding edges not scaling
                        b.Cursor = new Cursor(StandardCursorType.Hand);
                    }
                    else
                    {
                        // Restore cursors based on origin
                        if (b.Tag is ResizeOrigin origin)
                        {
                            switch (origin)
                            {
                                case ResizeOrigin.TopLeft: b.Cursor = new Cursor(StandardCursorType.TopLeftCorner); break;
                                case ResizeOrigin.TopRight: b.Cursor = new Cursor(StandardCursorType.TopRightCorner); break;
                                case ResizeOrigin.BottomLeft: b.Cursor = new Cursor(StandardCursorType.BottomLeftCorner); break;
                                case ResizeOrigin.BottomRight: b.Cursor = new Cursor(StandardCursorType.BottomRightCorner); break;
                            }
                        }
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    _selectionAdorner.IsVisible = value;
                    if (value) Cursor = new Cursor(StandardCursorType.Hand);
                    else Cursor = Cursor.Default;

                    if (!value && _isCropMode) 
                    {
                        ToggleCropMode(); // Exit crop mode when deselected
                    }
                }
            }
        }

        public void RotateRight90() => _rotateTransform.Angle += 90;
        public void RotateLeft90() => _rotateTransform.Angle -= 90;
        public void FlipHorizontal() => _scaleTransform.ScaleX *= -1;
        public void FlipVertical() => _scaleTransform.ScaleY *= -1;

        private void OnControlPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_isResizing) return;
            
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                Selected?.Invoke(this, EventArgs.Empty);
                
                if (_isSelected) 
                {
                    _isDraggingImage = true;
                    this.Opacity = 0.7;
                    _imageDragStartPoint = e.GetPosition((Visual?)Parent); 
                    ImageDragStarted?.Invoke(this, EventArgs.Empty);
                    e.Pointer.Capture(this);
                }
                e.Handled = true;
            }
        }

        private void OnControlPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isResizing) return;

            if (_isDraggingImage && Parent is Visual parentVisual)
            {
                var currentPoint = e.GetPosition(parentVisual);
                double deltaX = currentPoint.X - _imageDragStartPoint.X;
                double deltaY = currentPoint.Y - _imageDragStartPoint.Y;
                
                _imageDragStartPoint = currentPoint;

                ImageDragged?.Invoke(this, new ImageDragEventArgs(deltaX, deltaY, currentPoint));
                e.Handled = true;
            }
        }

        private void OnControlPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingImage)
            {
                _isDraggingImage = false;
                this.Opacity = 1.0;
                ImageDragEnded?.Invoke(this, EventArgs.Empty);
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void ApplyBounds()
        {
            this.Width = _baseWidth - _cropLeft - _cropRight;
            this.Height = _baseHeight - _cropTop - _cropBottom;
            
            _image.Width = _baseWidth;
            _image.Height = _baseHeight;
            _image.Margin = new Thickness(-_cropLeft, -_cropTop, 0, 0);
        }

        private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && sender is Border handle && handle.Tag is ResizeOrigin origin)
            {
                _isResizing = true;
                _activeResizeOrigin = origin;
                
                _resizeStartPointGlobal = e.GetPosition(null); 
                
                _originalWidth = _baseWidth;
                _originalHeight = _baseHeight;
                
                _originalCropLeft = _cropLeft;
                _originalCropTop = _cropTop;
                _originalCropRight = _cropRight;
                _originalCropBottom = _cropBottom;
                
                _originalCanvasLeft = double.IsNaN(Canvas.GetLeft(this)) ? 0 : Canvas.GetLeft(this);
                _originalCanvasTop = double.IsNaN(Canvas.GetTop(this)) ? 0 : Canvas.GetTop(this);

                e.Pointer.Capture((IInputElement?)sender);
                e.Handled = true;
            }
        }

        private void OnResizePointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isResizing && Parent is Canvas parentCanvas)
            {
                var currentGlobal = e.GetPosition(null);
                double deltaX = currentGlobal.X - _resizeStartPointGlobal.X;
                double deltaY = currentGlobal.Y - _resizeStartPointGlobal.Y;

                if (_isCropMode)
                {
                    // Update crop bounds
                    if (_activeResizeOrigin == ResizeOrigin.BottomRight)
                    {
                        _cropRight = Math.Max(0, _originalCropRight - deltaX);
                        _cropBottom = Math.Max(0, _originalCropBottom - deltaY);
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.TopLeft)
                    {
                        _cropLeft = Math.Max(0, _originalCropLeft + deltaX);
                        _cropTop = Math.Max(0, _originalCropTop + deltaY);
                        Canvas.SetLeft(this, _originalCanvasLeft + (_cropLeft - _originalCropLeft));
                        Canvas.SetTop(this, _originalCanvasTop + (_cropTop - _originalCropTop));
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.TopRight)
                    {
                        _cropRight = Math.Max(0, _originalCropRight - deltaX);
                        _cropTop = Math.Max(0, _originalCropTop + deltaY);
                        Canvas.SetTop(this, _originalCanvasTop + (_cropTop - _originalCropTop));
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.BottomLeft)
                    {
                        _cropLeft = Math.Max(0, _originalCropLeft + deltaX);
                        _cropBottom = Math.Max(0, _originalCropBottom - deltaY);
                        Canvas.SetLeft(this, _originalCanvasLeft + (_cropLeft - _originalCropLeft));
                    }
                }
                else
                {
                    // Standard scaling
                    double aspectRatio = _originalWidth / _originalHeight;
                    double newWidth = _originalWidth;
                    double newHeight = _originalHeight;

                    if (_activeResizeOrigin == ResizeOrigin.BottomRight)
                    {
                        newWidth = Math.Max(50, _originalWidth + deltaX);
                        newHeight = newWidth / aspectRatio;
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.TopLeft)
                    {
                        newWidth = Math.Max(50, _originalWidth - deltaX);
                        newHeight = newWidth / aspectRatio;

                        double widthDiff = _originalWidth - newWidth;
                        double heightDiff = _originalHeight - newHeight;
                        Canvas.SetLeft(this, _originalCanvasLeft + widthDiff);
                        Canvas.SetTop(this, _originalCanvasTop + heightDiff);
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.TopRight)
                    {
                        newWidth = Math.Max(50, _originalWidth + deltaX);
                        newHeight = newWidth / aspectRatio;

                        double heightDiff = _originalHeight - newHeight;
                        Canvas.SetTop(this, _originalCanvasTop + heightDiff);
                    }
                    else if (_activeResizeOrigin == ResizeOrigin.BottomLeft)
                    {
                        newWidth = Math.Max(50, _originalWidth - deltaX);
                        newHeight = newWidth / aspectRatio;

                        double widthDiff = _originalWidth - newWidth;
                        Canvas.SetLeft(this, _originalCanvasLeft + widthDiff);
                    }

                    // To preserve crop proportions while resizing the entire unit:
                    double scaleRatioX = newWidth / _originalWidth;
                    double scaleRatioY = newHeight / _originalHeight;
                    
                    _baseWidth = newWidth;
                    _baseHeight = newHeight;
                    
                    _cropLeft = _originalCropLeft * scaleRatioX;
                    _cropTop = _originalCropTop * scaleRatioY;
                    _cropRight = _originalCropRight * scaleRatioX;
                    _cropBottom = _originalCropBottom * scaleRatioY;
                }

                ApplyBounds();
                e.Handled = true;
            }
        }

        private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }
    }
}
