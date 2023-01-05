using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Edda;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Media;
using Image = System.Windows.Controls.Image;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Input;
using Point = System.Windows.Point;
using Edda.Const;

public class EditorUI {

    // constructor variables
    MainWindow parentWindow;
    public MapEditor mapEditor;
    Canvas EditorGrid;
    ScrollViewer scrollEditor;
    ColumnDefinition referenceCol;
    RowDefinition referenceRow;
    Border borderNavWaveform;
    ColumnDefinition colWaveformVertical;
    Image imgWaveformVertical;
    Grid editorMarginGrid;
    Canvas canvasNavInputBox;
    Canvas canvasBookmarks;
    Canvas canvasBookmarkLabels;
    Line lineSongMouseover;

    // dispatcher of the main application UI thread
    Dispatcher dispatcher;

    // user-defined settings 
    public double gridSpacing;
    public double gridOffset;
    public int gridDivision;
    public bool showWaveform;
    public bool snapToGrid = true;

    // dynamically added controls
    Border dragSelectBorder = new();
    Line lineGridMouseover = new();
    Canvas noteCanvas = new();
    Image imgAudioWaveform = new();
    Image imgPreviewNote = new();

    // editing grid
    bool mouseOutOfBounds;
    List<double> gridBeatLines = new();
    List<double> majorGridBeatLines = new();

    // waveform
    VorbisWaveformVisualiser audioWaveform;
    VorbisWaveformVisualiser navWaveform;

    // marker editing
    bool isEditingMarker = false;
    double markerDragOffset = 0;
    Canvas currentlyDraggingMarker;
    Bookmark currentlyDraggingBookmark;
    BPMChange currentlyDraggingBPMChange;

    // dragging variables
    bool isDragging = false;
    Point dragSelectStart;

    // grid measurements
    double unitLength {
        get { return referenceCol.ActualWidth * gridSpacing; }
    }
    double unitLengthUnscaled {
        get { return referenceCol.ActualWidth; }
    }
    double unitSubLength {
        get { return referenceCol.ActualWidth / 3; }
    }
    double unitHeight {
        get { return referenceRow.ActualHeight; }
    }
    double currentSeekBeat {
        get {
            return BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, snapToGrid);
        }
    }

    // info on currently selected beat/col from mouse position
    int mouseGridCol;
    double mouseBeatUnsnapped;
    double mouseBeatSnapped;
    public Note mouseNote {
        get {
            return new Note(mouseBeat, mouseColumn);
        }
    }
    public double mouseBeat {
        get {
            return snapToGrid ? snappedBeat : unsnappedBeat;
        }
    }
    public double snappedBeat {
        get {
            return mouseBeatSnapped;
        }
    }
    public double unsnappedBeat {
        get {
            return mouseBeatUnsnapped;
        }
    }
    public int mouseColumn {
        get {
            return mouseGridCol;
        }
    }
    public bool isMouseOnEditingGrid {
        get {
            return imgPreviewNote.Opacity > 0;
        }
    }

    // constructor
    public EditorUI (
        MainWindow parentWindow,
        MapEditor mapEditor,
        Canvas EditorGrid, 
        ScrollViewer scrollEditor, 
        ColumnDefinition referenceCol, 
        RowDefinition referenceRow,
        Border borderNavWaveform,
        ColumnDefinition colWaveformVertical,
        Image imgWaveformVertical,
        Grid editorMarginGrid,
        Canvas canvasNavInputBox,
        Canvas canvasBookmarks,
        Canvas canvasBookmarkLabels,
        Line lineSongMouseover
    ) {
        this.parentWindow = parentWindow;
        this.mapEditor = mapEditor;
        this.EditorGrid = EditorGrid;
        this.referenceCol = referenceCol;
        this.referenceRow = referenceRow;
        this.scrollEditor = scrollEditor;
        this.borderNavWaveform = borderNavWaveform;
        this.colWaveformVertical = colWaveformVertical;
        this.imgWaveformVertical = imgWaveformVertical;
        this.editorMarginGrid = editorMarginGrid;
        this.canvasNavInputBox = canvasNavInputBox;
        this.canvasBookmarks = canvasBookmarks;
        this.canvasBookmarkLabels = canvasBookmarkLabels;
        this.lineSongMouseover = lineSongMouseover;

        dispatcher = parentWindow.Dispatcher;

        RenderOptions.SetBitmapScalingMode(imgAudioWaveform, BitmapScalingMode.NearestNeighbor);

        noteCanvas.SetBinding(Canvas.WidthProperty, new Binding("ActualWidth") { Source = EditorGrid });
        noteCanvas.SetBinding(Canvas.HeightProperty, new Binding("ActualHeight") { Source = EditorGrid });

        imgPreviewNote.Opacity = Editor.PreviewNoteOpacity;
        imgPreviewNote.Width = unitLength;
        imgPreviewNote.Height = unitHeight;
        noteCanvas.Children.Add(imgPreviewNote);

        dragSelectBorder.BorderBrush = Brushes.Black;
        dragSelectBorder.BorderThickness = new Thickness(2);
        dragSelectBorder.Background = Brushes.LightBlue;
        dragSelectBorder.Opacity = 0.5;
        dragSelectBorder.Visibility = Visibility.Hidden;

        lineGridMouseover.Opacity = 0;
        lineGridMouseover.X1 = 0;
        lineGridMouseover.SetBinding(Line.X2Property, new Binding("ActualWidth") { Source = EditorGrid });
        lineGridMouseover.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.GridPreviewLine.Colour);
        lineGridMouseover.StrokeThickness = Editor.GridPreviewLine.Thickness;
        lineGridMouseover.Visibility = Visibility.Hidden;
        EditorGrid.Children.Add(lineGridMouseover);
    }

    public void SetMouseoverLinePosition(double newPos) {
        lineGridMouseover.Y1 = newPos;
        lineGridMouseover.Y2 = newPos;
    }
    public void SetSongMouseoverLinePosition(double newLinePos) {
        lineSongMouseover.Y1 = newLinePos;
        lineSongMouseover.Y2 = newLinePos;
    }
    public void SetMouseoverLineVisibility(Visibility newVis) {
        lineGridMouseover.Visibility = newVis;
    }
    public void SetPreviewNoteVisibility(Visibility newVis) {
        imgPreviewNote.Visibility = newVis;
    }
    public void SetPreviewNote(double bottom, double left, ImageSource source) {
        Canvas.SetBottom(imgPreviewNote, bottom);
        imgPreviewNote.Source = source;
        Canvas.SetLeft(imgPreviewNote, left);
    }

    // waveform drawing
    public void InitWaveforms(string songPath) {
        audioWaveform = new VorbisWaveformVisualiser(songPath);
        navWaveform = new VorbisWaveformVisualiser(songPath);
    }
    public void DrawMainWaveform() {
        if (!EditorGrid.Children.Contains(imgAudioWaveform)) {
            EditorGrid.Children.Add(imgAudioWaveform);
        }
        ResizeMainWaveform();
        double height = EditorGrid.Height - scrollEditor.ActualHeight;
        double width = EditorGrid.ActualWidth * Editor.Waveform.Width;
        CreateMainWaveform(height, width);
    }
    public void UndrawMainWaveform() {
        EditorGrid.Children.Remove(imgAudioWaveform);
    }
    private void ResizeMainWaveform() {
        imgAudioWaveform.Height = EditorGrid.Height - scrollEditor.ActualHeight;
        imgAudioWaveform.Width = EditorGrid.ActualWidth;
        Canvas.SetBottom(imgAudioWaveform, unitHeight / 2);
    }
    internal void DrawNavWaveform() {
        Task.Run(() => {
            DateTime before = DateTime.Now;
            ImageSource bmp = navWaveform.Draw(borderNavWaveform.ActualHeight, colWaveformVertical.ActualWidth);
            Trace.WriteLine($"INFO: Drew nav waveform in {(DateTime.Now - before).TotalSeconds} sec");
            if (bmp != null) {
                this.dispatcher.Invoke(() => {
                    imgWaveformVertical.Source = bmp;
                });
            }
        });
    }
    private void CreateMainWaveform(double height, double width) {
        Task.Run(() => {
            DateTime before = DateTime.Now;
            ImageSource bmp = audioWaveform.Draw(height, width);
            Trace.WriteLine($"INFO: Drew big waveform in {(DateTime.Now - before).TotalSeconds} sec");

            this.dispatcher.Invoke(() => {
                if (bmp != null && showWaveform) {
                    imgAudioWaveform.Source = bmp;
                    ResizeMainWaveform();
                }
            });
        });
    }

    // grid drawing
    public void UpdateGridHeight() {
        // resize editor grid height to fit scrollEditor height
        double beats = mapEditor.globalBPM / 60 * parentWindow.songTotalTimeInSeconds;
        EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;
    }
    public void DrawGrid(bool redrawWaveform = true) {
        // resize editor grid height to fit scrollEditor height
        double beats = mapEditor.globalBPM / 60 * parentWindow.songTotalTimeInSeconds;
        EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;

        EditorGrid.Children.Clear();

        DateTime start = DateTime.Now;

        // draw gridlines
        EditorGrid.Children.Add(lineGridMouseover);
        DrawGridLines(EditorGrid.Height - scrollEditor.ActualHeight);

        // then draw the waveform
        if (showWaveform) {
            EditorGrid.Children.Add(imgAudioWaveform);
        }
        if (redrawWaveform && showWaveform && EditorGrid.Height - scrollEditor.ActualHeight > 0) {
            DrawMainWaveform();
        }

        // then draw the notes
        noteCanvas.Children.Clear();
        DrawNotes(mapEditor.currentMapDifficulty.notes);

        // including the mouseover preview note
        imgPreviewNote.Width = unitLength;
        imgPreviewNote.Height = unitHeight;
        noteCanvas.Children.Add(imgPreviewNote);

        EditorGrid.Children.Add(noteCanvas);

        // then the drag selection rectangle
        EditorGrid.Children.Add(dragSelectBorder);

        // finally, draw the markers
        DrawBookmarks();
        DrawNavBookmarks();
        DrawBPMChanges();

        Trace.WriteLine($"INFO: Redrew editor grid in {(DateTime.Now - start).TotalSeconds} seconds.");
    }
    internal void DrawGridLines(double gridHeight) {
        // helper function for creating gridlines
        Line makeGridLine(double offset, bool isMajor = false) {
            var l = MakeLine(EditorGrid.ActualWidth, offset);
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(
                isMajor ? Editor.MajorGridlineColour : Editor.MinorGridlineColour)
            ;
            l.StrokeThickness = isMajor ? Editor.MajorGridlineThickness : Editor.MinorGridlineThickness;
            Canvas.SetBottom(l, offset + unitHeight / 2);
            return l;
        }

        majorGridBeatLines.Clear();
        gridBeatLines.Clear();

        // calculate grid offset
        double userOffset = gridOffset * mapEditor.globalBPM / 60 * unitLength;

        // the position to place gridlines, starting at the user-specified grid offset
        var offset = userOffset;

        var localBPM = mapEditor.globalBPM;
        var localGridDiv = gridDivision;

        // draw gridlines
        int counter = 0;
        int bpmChangeCounter = 0;
        while (offset <= gridHeight) {

            // add new gridline
            bool isMajor = counter % localGridDiv == 0;
            var l = makeGridLine(offset, isMajor);
            EditorGrid.Children.Add(l);
            if (isMajor) {
                majorGridBeatLines.Add((offset - userOffset) / unitLength);
            }
            gridBeatLines.Add((offset - userOffset) / unitLength);

            offset += mapEditor.globalBPM / localBPM * unitLength / localGridDiv;
            counter++;

            // check for BPM change
            if (bpmChangeCounter < mapEditor.currentMapDifficulty.bpmChanges.Count && Helper.DoubleApproxGreaterEqual((offset - userOffset) / unitLength, mapEditor.currentMapDifficulty.bpmChanges[bpmChangeCounter].globalBeat)) {
                BPMChange next = mapEditor.currentMapDifficulty.bpmChanges[bpmChangeCounter];

                offset = next.globalBeat * unitLength + userOffset;
                localBPM = next.BPM;
                localGridDiv = next.gridDivision;

                bpmChangeCounter++;
                counter = 0;
            }
        }
        //this.Dispatcher.Invoke(() => {
        //    foreach (var l in gridLinesUI) {
        //        EditorGrid.Children.Add(l);
        //    }
        //});
    }

    // marker drawing
    internal void DrawBookmarks() {
        foreach (Bookmark b in mapEditor.currentMapDifficulty.bookmarks) {
            Canvas bookmarkCanvas = new();
            Canvas.SetRight(bookmarkCanvas, 0);
            Canvas.SetBottom(bookmarkCanvas, unitLength * b.beat + unitHeight / 2);

            var l = MakeLine(EditorGrid.ActualWidth / 2, unitLength * b.beat);
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.GridBookmark.Colour);
            l.StrokeThickness = Editor.GridBookmark.Thickness;
            l.Opacity = Editor.GridBookmark.Opacity;
            Canvas.SetRight(l, 0);
            Canvas.SetBottom(l, 0);
            bookmarkCanvas.Children.Add(l);

            var txtBlock = new Label();
            txtBlock.Foreground = Brushes.White;
            txtBlock.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.GridBookmark.NameColour);
            txtBlock.Background.Opacity = Editor.GridBookmark.Opacity;
            txtBlock.Content = b.name;
            txtBlock.FontSize = Editor.GridBookmark.NameSize;
            txtBlock.Padding = new Thickness(Editor.GridBookmark.NamePadding);
            txtBlock.FontWeight = FontWeights.Bold;
            txtBlock.Opacity = 1.0;
            //txtBlock.IsReadOnly = true;
            txtBlock.Cursor = Cursors.Hand;
            Canvas.SetRight(txtBlock, 0);
            Canvas.SetBottom(txtBlock, 0.75 * Editor.GridBookmark.Thickness);
            txtBlock.MouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                e.Handled = true;
            });
            txtBlock.MouseLeftButtonUp += new MouseButtonEventHandler((src, e) => {
                parentWindow.songSeekPosition = b.beat / mapEditor.globalBPM * 60000;
                parentWindow.navMouseDown = false;
                e.Handled = true;
            });
            txtBlock.PreviewMouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                currentlyDraggingMarker = bookmarkCanvas;
                currentlyDraggingBookmark = b;
                currentlyDraggingBPMChange = null;
                markerDragOffset = e.GetPosition(bookmarkCanvas).Y;
                SetPreviewNoteVisibility(Visibility.Hidden);
                EditorGrid.CaptureMouse();
                e.Handled = true;
            });
            txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
                if (!(e.ChangedButton == MouseButton.Middle)) {
                    return;
                }
                var res = MessageBox.Show("Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes) {
                    mapEditor.RemoveBookmark(b);
                }
            });
            txtBlock.MouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                var txtBox = new TextBox();
                txtBox.Text = b.name;
                txtBox.FontSize = Editor.GridBookmark.NameSize;
                Canvas.SetRight(txtBox, Editor.GridBookmark.NamePadding);
                Canvas.SetBottom(txtBox, Canvas.GetBottom(bookmarkCanvas) + Editor.GridBookmark.NamePadding);
                txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                    if (txtBox.Text != "") {
                        mapEditor.RenameBookmark(b, txtBox.Text);
                    }
                    EditorGrid.Children.Remove(txtBox);
                });
                txtBox.KeyDown += new KeyEventHandler((src, e) => {
                    if (e.Key == Key.Escape || e.Key == Key.Enter) {
                        Keyboard.ClearFocus();
                        Keyboard.Focus(parentWindow);
                    }
                });

                EditorGrid.Children.Add(txtBox);
                txtBox.Focus();
                txtBox.SelectAll();

                e.Handled = true;
            });
            bookmarkCanvas.Children.Add(txtBlock);


            EditorGrid.Children.Add(bookmarkCanvas);
        }
    }
    internal void DrawBPMChanges() {
        Label makeBPMChangeLabel(string content) {
            var label = new Label();
            label.Foreground = Brushes.White;
            label.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.BPMChange.NameColour);
            label.Background.Opacity = Editor.BPMChange.Opacity;
            label.Content = content;
            label.FontSize = Editor.BPMChange.NameSize;
            label.Padding = new Thickness(Editor.BPMChange.NamePadding);
            label.FontWeight = FontWeights.Bold;
            label.Opacity = 1.0;
            label.Cursor = Cursors.Hand;
            return label;
        }
        BPMChange prev = new BPMChange(0, mapEditor.globalBPM, gridDivision);
        foreach (BPMChange b in mapEditor.currentMapDifficulty.bpmChanges) {
            Canvas bpmChangeCanvas = new();
            Canvas bpmChangeFlagCanvas = new();

            var line = MakeLine(EditorGrid.ActualWidth / 2, unitLength * b.globalBeat);
            line.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.BPMChange.Colour);
            line.StrokeThickness = Editor.BPMChange.Thickness;
            line.Opacity = Editor.BPMChange.Opacity;
            Canvas.SetBottom(line, 0);
            bpmChangeCanvas.Children.Add(line);

            var divLabel = makeBPMChangeLabel($"1/{b.gridDivision} beat");
            divLabel.PreviewMouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                isEditingMarker = true;
                var txtBox = new TextBox();
                txtBox.Text = b.gridDivision.ToString();
                txtBox.FontSize = Editor.BPMChange.NameSize;
                Canvas.SetLeft(txtBox, 12);
                Canvas.SetBottom(txtBox, line.StrokeThickness + 2);
                txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                    int div;
                    if (int.TryParse(txtBox.Text, out div) && Helper.DoubleRangeCheck(div, 1, Editor.GridDivisionMax)) {
                        mapEditor.RemoveBPMChange(b, false);
                        b.gridDivision = div;
                        mapEditor.AddBPMChange(b);
                    }
                    isEditingMarker = false;
                    parentWindow.Cursor = Cursors.Arrow;
                    canvasNavInputBox.Children.Remove(txtBox);
                });
                txtBox.KeyDown += new KeyEventHandler((src, e) => {
                    if (e.Key == Key.Escape || e.Key == Key.Enter) {
                        Keyboard.ClearFocus();
                        Keyboard.Focus(parentWindow);
                    }
                });

                bpmChangeCanvas.Children.Add(txtBox);
                txtBox.Focus();
                txtBox.SelectAll();

                e.Handled = true;
            });
            Canvas.SetBottom(divLabel, line.StrokeThickness);
            bpmChangeFlagCanvas.Children.Add(divLabel);

            var bpmLabel = makeBPMChangeLabel($"{b.BPM} BPM");
            bpmLabel.PreviewMouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                isEditingMarker = true;
                var txtBox = new TextBox();
                txtBox.Text = b.BPM.ToString();
                txtBox.FontSize = Editor.BPMChange.NameSize;
                Canvas.SetLeft(txtBox, 2);
                Canvas.SetBottom(txtBox, line.StrokeThickness + 22);
                txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                    double BPM;
                    if (double.TryParse(txtBox.Text, out BPM) && BPM > 0) {
                        mapEditor.RemoveBPMChange(b, false);
                        b.BPM = BPM;
                        mapEditor.AddBPMChange(b);
                    }
                    isEditingMarker = false;
                    parentWindow.Cursor = Cursors.Arrow;
                    canvasNavInputBox.Children.Remove(txtBox);
                });
                txtBox.KeyDown += new KeyEventHandler((src, e) => {
                    if (e.Key == Key.Escape || e.Key == Key.Enter) {
                        Keyboard.ClearFocus();
                        Keyboard.Focus(parentWindow);
                    }
                });

                bpmChangeCanvas.Children.Add(txtBox);
                txtBox.Focus();
                txtBox.SelectAll();

                e.Handled = true;
            });
            Canvas.SetBottom(bpmLabel, line.StrokeThickness + 20);
            bpmChangeFlagCanvas.Children.Add(bpmLabel);

            bpmChangeFlagCanvas.PreviewMouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                currentlyDraggingMarker = bpmChangeCanvas;
                currentlyDraggingBPMChange = b;
                currentlyDraggingBookmark = null;
                markerDragOffset = e.GetPosition(bpmChangeCanvas).Y;
                SetPreviewNoteVisibility(Visibility.Hidden);
                EditorGrid.CaptureMouse();
                e.Handled = true;
            });
            bpmChangeFlagCanvas.PreviewMouseDown += new MouseButtonEventHandler((src, e) => {
                if (!(e.ChangedButton == MouseButton.Middle)) {
                    return;
                }
                var res = MessageBox.Show("Are you sure you want to delete this timing change?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes) {
                    mapEditor.RemoveBPMChange(b);
                }
                e.Handled = true;
            });
            Canvas.SetBottom(bpmChangeFlagCanvas, 0);
            bpmChangeCanvas.Children.Add(bpmChangeFlagCanvas);

            Canvas.SetLeft(bpmChangeCanvas, 0);
            Canvas.SetBottom(bpmChangeCanvas, unitLength * b.globalBeat + unitHeight / 2);
            EditorGrid.Children.Add(bpmChangeCanvas);

            prev = b;
        }
    }
    internal void DrawNavBookmarks() {
        canvasBookmarks.Children.Clear();
        canvasBookmarkLabels.Children.Clear();
        foreach (Bookmark b in mapEditor.currentMapDifficulty.bookmarks) {
            var l = MakeLine(borderNavWaveform.ActualWidth, borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (mapEditor.globalBPM * parentWindow.songTotalTimeInSeconds * 1000)));
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.NavBookmark.Colour);
            l.StrokeThickness = Editor.NavBookmark.Thickness;
            l.Opacity = Editor.NavBookmark.Opacity;
            canvasBookmarks.Children.Add(l);

            var txtBlock = CreateBookmarkLabel(b);
            canvasBookmarkLabels.Children.Add(txtBlock);
        }
    }

    // note drawing
    internal void DrawNotes(List<Note> notes) {
        // draw drum notes
        // TODO: paginate these? they cause lag when resizing

        // init drum note image
        foreach (var n in notes) {
            var img = new Image();
            img.Width = unitLengthUnscaled;
            img.Height = unitHeight;

            var noteHeight = n.beat * unitLength;
            var noteXOffset = (1 + 4 * n.col) * unitSubLength;

            // find which beat fraction this note lies on
            img.Source = RuneForBeat(n.beat);

            // this assumes there are no duplicate notes given to us
            img.Uid = Helper.UidGenerator(n);
            var name = Helper.NameGenerator(n);

            if (parentWindow.FindName(name) != null) {
                parentWindow.UnregisterName(name);
            }
            parentWindow.RegisterName(name, img);

            Canvas.SetLeft(img, noteXOffset + editorMarginGrid.Margin.Left);
            Canvas.SetBottom(img, noteHeight);

            noteCanvas.Children.Add(img);
        }
    }
    internal void DrawNotes(Note n) {
        DrawNotes(new List<Note>() { n });
    }
    internal void UndrawNotes(List<Note> notes) {
        foreach (Note n in notes) {
            var nUid = Helper.UidGenerator(n);
            foreach (UIElement u in noteCanvas.Children) {
                if (u.Uid == nUid) {
                    noteCanvas.Children.Remove(u);
                    break;
                }
            }
        }
    }
    internal void UndrawNotes(Note n) {
        UndrawNotes(new List<Note>() { n });
    }
    internal void HighlightNotes(List<Note> notes) {
        foreach (Note n in notes) {
            foreach (UIElement e in noteCanvas.Children) {
                if (e.Uid == Helper.UidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat, true);
                }
            }
        }
    }
    internal void HighlightNotes(Note n) {
        HighlightNotes(new List<Note>() { n });
    }
    internal void UnhighlightNotes(List<Note> notes) {
        foreach (Note n in notes) {
            foreach (UIElement e in noteCanvas.Children) {
                if (e.Uid == Helper.UidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat);
                }
            }
        }
    }
    internal void UnhighlightNotes(Note n) {
        UnhighlightNotes(new List<Note>() { n });
    }
    internal List<double> GetBeats() {
        return majorGridBeatLines;
    }
    // mouse input handling
    internal void GridMouseMove(Point mousePos, bool snapMouseMovements) {
        // check if mouse is out of bounds of the song map
        mouseOutOfBounds = mousePos.Y < scrollEditor.ActualHeight - unitHeight / 2;
        
        UpdateMousePosition(mousePos);

        double noteX = (1 + 4 * mouseGridCol) * unitSubLength;
        // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
        var unknownNoteXAdjustment = (unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2;

        double userOffsetBeat = gridOffset * mapEditor.globalBPM / 60;
        double userOffset = userOffsetBeat * unitLength;
        var adjustedMousePos = EditorGrid.ActualHeight - mousePos.Y - unitHeight / 2;
        double gridLength = unitLength / gridDivision;

        // calculate column
        mouseGridCol = ColForPosition(mousePos.X);

        if (mouseOutOfBounds) {
            SetMouseoverLineVisibility(Visibility.Hidden);
            SetPreviewNoteVisibility(Visibility.Hidden);
        } else {
            SetMouseoverLineVisibility(Visibility.Visible);


            // set preview note visibility
            if (!isDragging) {
                if (mouseGridCol < 0 || mouseGridCol > 3) {
                    SetPreviewNoteVisibility(Visibility.Hidden);
                } else {
                    SetPreviewNoteVisibility(Visibility.Visible);
                }
            }

            // place preview note   
            double previewNoteBottom = snapToGrid ? (mouseBeatSnapped * gridLength * gridDivision + userOffset) : Math.Max(adjustedMousePos, userOffset);
            ImageSource previewNoteSource = RuneForBeat(userOffsetBeat + (snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped));
            double previewNoteLeft = noteX - unknownNoteXAdjustment + editorMarginGrid.Margin.Left;
            SetPreviewNote(previewNoteBottom, previewNoteLeft, previewNoteSource);

            // place preview line
            SetMouseoverLinePosition(mousePos.Y - markerDragOffset);
        }

        // move markers if one is being dragged right now
        if (!mouseOutOfBounds && currentlyDraggingMarker != null && !isEditingMarker) {
            MoveMarker(mousePos, snapMouseMovements);
            parentWindow.Cursor = Cursors.Hand;
        // otherwise, update existing drag operations
        } else if (isDragging) {
            UpdateDragSelection(mousePos);
        }
    }
    internal void GridMouseUp(Point mousePos, bool snapMouseMovements) {

        if (mouseGridCol >= 0 && mouseGridCol < 4) {
            SetPreviewNoteVisibility(Visibility.Visible);
        }
        if (currentlyDraggingMarker != null && !isEditingMarker) {
            var markerPos = mousePos;
            if (mouseOutOfBounds) {
                markerPos.Y = scrollEditor.ActualHeight - unitHeight / 2;
            }
            FinaliseMarkerEdit(markerPos, snapMouseMovements);
        } else if (isDragging) {
            EndDragSelection(mousePos, snapMouseMovements);
        } else if (!mouseOutOfBounds && EditorGrid.IsMouseCaptured && mouseGridCol >= 0 && mouseGridCol < 4) {

            Note n = new Note(mouseBeat, mouseGridCol);

            // select the note if it exists
            if (mapEditor.currentMapDifficulty.notes.Contains(n)) {
                if (snapMouseMovements) {
                    mapEditor.ToggleSelection(n);
                } else {
                    mapEditor.SelectNewNotes(n);
                }
            // otherwise create and add it
            } else {
                mapEditor.AddNotes(n);
                parentWindow.drummer.Play(n.col);
            }
        }

        EditorGrid.ReleaseMouseCapture();
        isDragging = false;
    }
    internal void GridMouseDown(Point mousePos) {
        dragSelectStart = mousePos;
        EditorGrid.CaptureMouse();
    }
    internal void BeginDragSelection(Point mousePos) {
        if (isDragging || (currentlyDraggingMarker != null && !isEditingMarker)) {
            return; 
        }
        imgPreviewNote.Visibility = Visibility.Hidden;
        dragSelectBorder.Visibility = Visibility.Visible;
        UpdateDragSelection(mousePos);
        dragSelectBorder.Width = 0;
        dragSelectBorder.Height = 0;
        isDragging = true;
    }
    internal void EndDragSelection(Point mousePos, bool snapMouseMovements) {
        dragSelectBorder.Visibility = Visibility.Hidden;
        // calculate new selections
        List<Note> newSelection = new List<Note>();
        double startBeat = BeatForPosition(dragSelectStart.Y, false);
        double endBeat = mouseBeatUnsnapped;
        int startCol = ColForPosition(dragSelectStart.X);
        int endCol = mouseGridCol;
        if (endCol == -1) {
            endCol = mousePos.X < EditorGrid.ActualWidth / 2 ? 0 : 3;
        }
        foreach (Note n in mapEditor.currentMapDifficulty.notes) {
            // minor optimisation
            if (n.beat > Math.Max(startBeat, endBeat)) {
                break;
            }
            // check range
            if (Helper.DoubleRangeCheck(n.beat, startBeat, endBeat) && Helper.DoubleRangeCheck(n.col, startCol, endCol)) {
                newSelection.Add(n);
            }
        }
        if (snapMouseMovements) {
            mapEditor.SelectNotes(newSelection);
        } else {
            mapEditor.SelectNewNotes(newSelection);
        }
    }
    internal void MoveMarker(Point mousePos, bool shiftKeyDown) {
        double newBottom = unitLength * BeatForPosition(mousePos.Y - markerDragOffset, shiftKeyDown);
        Canvas.SetBottom(currentlyDraggingMarker, newBottom + unitHeight / 2);
        SetMouseoverLineVisibility(Visibility.Visible);
    }
    private void FinaliseMarkerEdit(Point mousePos, bool snapMouseMovements) {
        if (currentlyDraggingBPMChange == null) {
            EditBookmark(BeatForPosition(mousePos.Y - markerDragOffset, snapMouseMovements));
        } else {
            mapEditor.RemoveBPMChange(currentlyDraggingBPMChange, false);
            currentlyDraggingBPMChange.globalBeat = BeatForPosition(mousePos.Y - markerDragOffset, snapMouseMovements);
            mapEditor.AddBPMChange(currentlyDraggingBPMChange);
            DrawGrid(false);
        }
        parentWindow.Cursor = Cursors.Arrow;
        SetMouseoverLineVisibility(Visibility.Hidden);
        currentlyDraggingBPMChange = null;
        currentlyDraggingMarker = null;
        markerDragOffset = 0;
    }
    private void UpdateMousePosition(Point mousePos) {
        // calculate beat
        try {
            mouseBeatSnapped = BeatForPosition(mousePos.Y, true);
            mouseBeatUnsnapped = BeatForPosition(mousePos.Y, false);
        } catch {
            mouseBeatSnapped = 0;
            mouseBeatUnsnapped = 0;
        }
    }
    private void EditBookmark(double beat) {
        mapEditor.RemoveBookmark(currentlyDraggingBookmark);
        currentlyDraggingBookmark.beat = beat;
        mapEditor.AddBookmark(currentlyDraggingBookmark);
        DrawGrid(false);
    }

    // keyboard shortcut functions
    internal void PasteClipboardWithOffset() {
        mapEditor.PasteClipboard(mouseBeatSnapped);
    }
    internal void CreateBookmark(bool onMouse = true) {
        double beat = currentSeekBeat;
        if (onMouse) {
            if (isMouseOnEditingGrid) {
                beat = snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped;
                // add bookmark on nav waveform
            } else if (lineSongMouseover.Opacity > 0) {
                beat = mapEditor.globalBPM * parentWindow.songTotalTimeInSeconds / 60000 * (1 - lineSongMouseover.Y1 / borderNavWaveform.ActualHeight);
            }
        }
        mapEditor.AddBookmark(new Bookmark(beat, Editor.NavBookmark.DefaultName));
    }
    internal void CreateBPMChange(bool snappedToGrid, bool onMouse = true) {
        double beat = (snappedToGrid) ? mouseBeatSnapped : mouseBeatUnsnapped;
        if (!onMouse) {
            beat = currentSeekBeat;
        }
        BPMChange previous = new BPMChange(0, mapEditor.globalBPM, gridDivision);
        foreach (var b in mapEditor.currentMapDifficulty.bpmChanges) {
            if (b.globalBeat < beat) {
                previous = b;
            }
        }
        mapEditor.AddBPMChange(new BPMChange(beat, previous.BPM, previous.gridDivision));
    }
    internal void CreateNote(int col, bool onMouse) {
        double mouseInput = snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped;
        Note n = new Note(onMouse ? mouseInput: currentSeekBeat, col);
        mapEditor.AddNotes(n);
    }
    
    // helper functions
    private Line MakeLine(double width, double offset) {
        var l = new Line();
        l.X1 = 0;
        l.X2 = width;
        l.Y1 = offset;
        l.Y2 = offset;
        return l;
    }
    private BitmapImage RuneForBeat(double beat, bool highlight = false) {
        // find most recent BPM change
        double recentBPMChange = 0;
        double recentBPM = mapEditor.globalBPM;
        foreach (var bc in mapEditor.currentMapDifficulty.bpmChanges) {
            if (Helper.DoubleApproxGreaterEqual(beat, bc.globalBeat)) {
                recentBPMChange = bc.globalBeat;
                recentBPM = bc.BPM;
            } else {
                break;
            }
        }
        double beatNormalised = beat - recentBPMChange;
        beatNormalised /= mapEditor.globalBPM / recentBPM;
        beatNormalised -= (int)beatNormalised;
        return Helper.BitmapImageForBeat(beatNormalised, highlight);
    }
    private Label CreateBookmarkLabel(Bookmark b) {
        var offset = borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (mapEditor.globalBPM * parentWindow.songTotalTimeInSeconds * 1000));
        var txtBlock = new Label();
        txtBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.NavBookmark.NameColour);

        txtBlock.Content = b.name;
        txtBlock.FontSize = Editor.NavBookmark.NameSize;
        txtBlock.Padding = new Thickness(Editor.NavBookmark.NamePadding);
        txtBlock.FontWeight = FontWeights.Bold;
        txtBlock.Opacity = Editor.NavBookmark.Opacity;
        //txtBlock.IsReadOnly = true;
        txtBlock.Cursor = Cursors.Hand;
        Canvas.SetBottom(txtBlock, borderNavWaveform.ActualHeight - offset);
        txtBlock.MouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
            e.Handled = true;
        });
        txtBlock.MouseLeftButtonUp += new MouseButtonEventHandler((src, e) => {
            parentWindow.songSeekPosition = b.beat / mapEditor.globalBPM * 60000;
            parentWindow.navMouseDown = false;
            e.Handled = true;
        });
        txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
            if (!(e.ChangedButton == MouseButton.Middle)) {
                return;
            }
            var res = MessageBox.Show("Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes) {
                mapEditor.RemoveBookmark(b);
            }
        });
        txtBlock.MouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
            var txtBox = new TextBox();
            txtBox.Text = b.name;
            txtBox.FontSize = Editor.NavBookmark.NameSize;
            Canvas.SetBottom(txtBox, borderNavWaveform.ActualHeight - offset);
            txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                if (txtBox.Text != "") {
                    mapEditor.RenameBookmark(b, txtBox.Text);
                }
                canvasNavInputBox.Children.Remove(txtBox);
            });
            txtBox.KeyDown += new KeyEventHandler((src, e) => {
                if (e.Key == Key.Escape || e.Key == Key.Enter) {
                    Keyboard.ClearFocus();
                    Keyboard.Focus(parentWindow);
                }
            });

            canvasNavInputBox.Children.Add(txtBox);
            txtBox.Focus();
            txtBox.SelectAll();

            e.Handled = true;
        });
        return txtBlock;
    }
    private int ColForPosition(double pos) {
        // calculate horizontal element
        var subLength = (pos - editorMarginGrid.Margin.Left) / unitSubLength;
        int col = -1;
        if (0 <= subLength && subLength <= 4.5) {
            col = 0;
        } else if (4.5 <= subLength && subLength <= 8.5) {
            col = 1;
        } else if (8.5 <= subLength && subLength <= 12.5) {
            col = 2;
        } else if (12.5 <= subLength && subLength <= 17.0) {
            col = 3;
        } else if (17.0 < subLength) {
            col = 4;
        }
        return col;
    }
    private double BeatForPosition(double position, bool snap) {
        double userOffsetBeat = gridOffset * mapEditor.globalBPM / 60;
        double userOffset = userOffsetBeat * unitLength;
        var pos = EditorGrid.ActualHeight - position - unitHeight / 2;
        double gridLength = unitLength / gridDivision;
        // check if mouse position would correspond to a negative row index
        double snapped = 0;
        double unsnapped = 0;
        if (pos >= userOffset) {
            unsnapped = (pos - userOffset) / unitLength;
            int binarySearch = gridBeatLines.BinarySearch(unsnapped);
            if (binarySearch > 0) {
                return gridBeatLines[binarySearch];
            }
            int indx1 = Math.Min(gridBeatLines.Count - 1, - binarySearch - 1);
            int indx2 = Math.Max(0, indx1 - 1);
            snapped = (gridBeatLines[indx1] - unsnapped) < (unsnapped - gridBeatLines[indx2]) ? gridBeatLines[indx1] : gridBeatLines[indx2];
        }
        return snap ? snapped : unsnapped;
    }
    private void UpdateDragSelection(Point newPoint) {
        Point p1;
        p1.X = Math.Min(newPoint.X, dragSelectStart.X);
        p1.Y = Math.Min(newPoint.Y, dragSelectStart.Y);
        Point p2;
        p2.X = Math.Max(newPoint.X, dragSelectStart.X);
        p2.Y = Math.Max(newPoint.Y, dragSelectStart.Y);
        Vector delta = p2 - p1;
        Canvas.SetLeft(dragSelectBorder, p1.X);
        Canvas.SetTop(dragSelectBorder, p1.Y);
        dragSelectBorder.Width = delta.X;
        dragSelectBorder.Height = delta.Y;
    }
}
