using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Edda;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Media;
using Image = System.Windows.Controls.Image;
using System.Windows.Threading;
using static Const.Editor;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Input;
using Point = System.Windows.Point;
using Const;

public class EditorUI {
    MainWindow parentWindow;
    Canvas EditorGrid;
    ScrollViewer scrollEditor;
    Border borderNavWaveform;
    ColumnDefinition colWaveformVertical;
    Image imgWaveformVertical;
    Grid editorMarginGrid;
    Canvas canvasNavInputBox;
    Canvas canvasBookmarks;
    Canvas canvasBookmarkLabels;
    Line lineSongMouseover;

    ColumnDefinition referenceCol;
    RowDefinition referenceRow;

    public MapEditor mapEditor;

    public double gridSpacing;
    public double gridOffset;
    public int gridDivision;
    public bool showWaveform;
    double markerDragOffset = 0;
    Point dragSelectStart;
    bool isEditingMarker = false;

    double editorSelBeatStart;
    int editorSelColStart;

    Border editorDragSelectBorder = new();
    Line lineGridMouseover = new();
    Canvas EditorGridNoteCanvas = new();
    Image imgAudioWaveform = new();
    Image imgPreviewNote = new();
    List<double> gridlines = new();
    List<double> majorGridlines = new();
    public bool isMouseOnEditingGrid {
        get {
            return imgPreviewNote.Opacity > 0;
        }
    }
    Dispatcher dispatcher;
    VorbisWaveformVisualiser audioWaveform;
    VorbisWaveformVisualiser navWaveform;

    Canvas currentlyDraggingMarker;
    Bookmark currentlyDraggingBookmark;
    BPMChange currentlyDraggingBPMChange;

    int editorMouseGridCol;
    double editorMouseBeatUnsnapped;
    double editorMouseBeatSnapped;
    internal bool editorSnapToGrid = true;

    bool editorIsDragging = false;

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
    public Note mouseNote {
        get {
            return new Note(mouseBeat, mouseColumn);
        }
    }
    public double mouseBeat {
        get {
            return editorSnapToGrid ? snappedBeat : unsnappedBeat;
        }
    }
    public double snappedBeat {
        get {
            return editorMouseBeatSnapped;
        }
    }
    public double unsnappedBeat {
        get {
            return editorMouseBeatUnsnapped;
        }
    }
    public int mouseColumn {
        get {
            return editorMouseGridCol;
        }
    }
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

        EditorGridNoteCanvas.SetBinding(Canvas.WidthProperty, new Binding("ActualWidth") { Source = EditorGrid });
        EditorGridNoteCanvas.SetBinding(Canvas.HeightProperty, new Binding("ActualHeight") { Source = EditorGrid });

        imgPreviewNote.Opacity = Const.Editor.PreviewNoteOpacity;
        imgPreviewNote.Width = unitLength;
        imgPreviewNote.Height = unitHeight;
        EditorGridNoteCanvas.Children.Add(imgPreviewNote);

        editorDragSelectBorder.BorderBrush = Brushes.Black;
        editorDragSelectBorder.BorderThickness = new Thickness(2);
        editorDragSelectBorder.Background = Brushes.LightBlue;
        editorDragSelectBorder.Opacity = 0.5;
        editorDragSelectBorder.Visibility = Visibility.Hidden;

        lineGridMouseover.Opacity = 0;
        lineGridMouseover.X1 = 0;
        lineGridMouseover.SetBinding(Line.X2Property, new Binding("ActualWidth") { Source = EditorGrid });
        lineGridMouseover.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridPreviewLine.Colour);
        lineGridMouseover.StrokeThickness = Const.Editor.GridPreviewLine.Thickness;
        lineGridMouseover.Visibility = Visibility.Hidden;
        EditorGrid.Children.Add(lineGridMouseover);
    }

    public void InitWaveforms(string songPath) {
        audioWaveform = new VorbisWaveformVisualiser(songPath);
        navWaveform = new VorbisWaveformVisualiser(songPath);
    }
    public void SetMouseoverLinePosition(double newPos) {
        lineGridMouseover.Y1 = newPos;
        lineGridMouseover.Y2 = newPos;
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
    public void UpdateEditorGridHeight() {
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
        DrawGridLines(EditorGrid.Height);

        // then draw the waveform
        if (redrawWaveform && showWaveform && EditorGrid.Height - scrollEditor.ActualHeight > 0) {
            DrawMainWaveform();
        }
        EditorGrid.Children.Add(imgAudioWaveform);

        // then draw the notes
        EditorGridNoteCanvas.Children.Clear();
        DrawEditorNotes(mapEditor.currentMapDifficulty.notes);

        // including the mouseover preview note
        imgPreviewNote.Width = unitLength;
        imgPreviewNote.Height = unitHeight;
        EditorGridNoteCanvas.Children.Add(imgPreviewNote);

        EditorGrid.Children.Add(EditorGridNoteCanvas);

        // then the drag selection rectangle
        EditorGrid.Children.Add(editorDragSelectBorder);

        // finally, draw the markers
        DrawEditorGridBookmarks();
        DrawEditorGridBPMChanges();

        Trace.WriteLine($"INFO: Redrew editor grid in {(DateTime.Now - start).TotalSeconds} seconds.");
    }
    internal void DrawGridLines(double gridHeight) {
        // helper function for creating gridlines
        Line makeGridLine(double offset, bool isMajor = false) {
            var l = makeLine(EditorGrid.ActualWidth, offset);
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(
                isMajor ? Const.Editor.MajorGridlineColour : Const.Editor.MinorGridlineColour)
            ;
            l.StrokeThickness = isMajor ? Const.Editor.MajorGridlineThickness : Const.Editor.MinorGridlineThickness;
            Canvas.SetBottom(l, offset + unitHeight / 2);
            return l;
        }

        majorGridlines.Clear();
        gridlines.Clear();

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
                majorGridlines.Add((offset - userOffset) / unitLength);
            }
            gridlines.Add((offset - userOffset) / unitLength);

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
    public void CreateMainWaveform(double height, double width) {
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
    public void ResizeMainWaveform() {
        imgAudioWaveform.Height = EditorGrid.Height - scrollEditor.ActualHeight;
        imgAudioWaveform.Width = EditorGrid.ActualWidth;
        Canvas.SetBottom(imgAudioWaveform, unitHeight / 2);
    }
    public void DrawMainWaveform() {
        if (!EditorGrid.Children.Contains(imgAudioWaveform)) {
            EditorGrid.Children.Add(imgAudioWaveform);
        }
        ResizeMainWaveform();
        double height = EditorGrid.Height - scrollEditor.ActualHeight;
        double width = EditorGrid.ActualWidth * Const.Editor.Waveform.Width;
        CreateMainWaveform(height, width);
    }
    public void UndrawMainWaveform() {
        EditorGrid.Children.Remove(imgAudioWaveform);
    }
    internal void DrawEditorNavWaveform() {
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
    internal void DrawEditorNotes(List<Note> notes) {
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

            //if (FindName(name) != null) {
            //    UnregisterName(name);
            //}
            //RegisterName(name, img);

            Canvas.SetLeft(img, noteXOffset + editorMarginGrid.Margin.Left);
            Canvas.SetBottom(img, noteHeight);

            EditorGridNoteCanvas.Children.Add(img);
        }
    }
    internal void DrawEditorNotes(Note n) {
        DrawEditorNotes(new List<Note>() { n });
    }
    internal void DrawEditorGridBookmarks() {
        foreach (Bookmark b in mapEditor.currentMapDifficulty.bookmarks) {
            Canvas bookmarkCanvas = new();
            Canvas.SetRight(bookmarkCanvas, 0);
            Canvas.SetBottom(bookmarkCanvas, unitLength * b.beat + unitHeight / 2);

            var l = makeLine(EditorGrid.ActualWidth / 2, unitLength * b.beat);
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridBookmark.Colour);
            l.StrokeThickness = Const.Editor.GridBookmark.Thickness;
            l.Opacity = Const.Editor.GridBookmark.Opacity;
            Canvas.SetRight(l, 0);
            Canvas.SetBottom(l, 0);
            bookmarkCanvas.Children.Add(l);

            var txtBlock = new Label();
            txtBlock.Foreground = Brushes.White;
            txtBlock.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.GridBookmark.NameColour);
            txtBlock.Background.Opacity = Const.Editor.GridBookmark.Opacity;
            txtBlock.Content = b.name;
            txtBlock.FontSize = Const.Editor.GridBookmark.NameSize;
            txtBlock.Padding = new Thickness(Const.Editor.GridBookmark.NamePadding);
            txtBlock.FontWeight = FontWeights.Bold;
            txtBlock.Opacity = 1.0;
            //txtBlock.IsReadOnly = true;
            txtBlock.Cursor = Cursors.Hand;
            Canvas.SetRight(txtBlock, 0);
            Canvas.SetBottom(txtBlock, 0.75 * Const.Editor.GridBookmark.Thickness);
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
                txtBox.FontSize = Const.Editor.GridBookmark.NameSize;
                Canvas.SetRight(txtBox, Const.Editor.GridBookmark.NamePadding);
                Canvas.SetBottom(txtBox, Canvas.GetBottom(bookmarkCanvas) + Const.Editor.GridBookmark.NamePadding);
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
    internal void DrawEditorGridBPMChanges() {
        Label makeBPMChangeLabel(string content) {
            var label = new Label();
            label.Foreground = Brushes.White;
            label.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.BPMChange.NameColour);
            label.Background.Opacity = Const.Editor.BPMChange.Opacity;
            label.Content = content;
            label.FontSize = Const.Editor.BPMChange.NameSize;
            label.Padding = new Thickness(Const.Editor.BPMChange.NamePadding);
            label.FontWeight = FontWeights.Bold;
            label.Opacity = 1.0;
            label.Cursor = Cursors.Hand;
            return label;
        }
        BPMChange prev = new BPMChange(0, mapEditor.globalBPM, gridDivision);
        foreach (BPMChange b in mapEditor.currentMapDifficulty.bpmChanges) {
            Canvas bpmChangeCanvas = new();
            Canvas bpmChangeFlagCanvas = new();

            var line = makeLine(EditorGrid.ActualWidth / 2, unitLength * b.globalBeat);
            line.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.BPMChange.Colour);
            line.StrokeThickness = Const.Editor.BPMChange.Thickness;
            line.Opacity = Const.Editor.BPMChange.Opacity;
            Canvas.SetBottom(line, 0);
            bpmChangeCanvas.Children.Add(line);

            var divLabel = makeBPMChangeLabel($"1/{b.gridDivision} beat");
            divLabel.PreviewMouseRightButtonUp += new MouseButtonEventHandler((src, e) => {
                isEditingMarker = true;
                var txtBox = new TextBox();
                txtBox.Text = b.gridDivision.ToString();
                txtBox.FontSize = Const.Editor.BPMChange.NameSize;
                Canvas.SetLeft(txtBox, 12);
                Canvas.SetBottom(txtBox, line.StrokeThickness + 2);
                txtBox.LostKeyboardFocus += new KeyboardFocusChangedEventHandler((src, e) => {
                    int div;
                    if (int.TryParse(txtBox.Text, out div) && Helper.DoubleRangeCheck(div, 1, Const.Editor.GridDivisionMax)) {
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
                txtBox.FontSize = Const.Editor.BPMChange.NameSize;
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
            var l = makeLine(borderNavWaveform.ActualWidth, borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (mapEditor.globalBPM * parentWindow.songTotalTimeInSeconds * 1000)));
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.NavBookmark.Colour);
            l.StrokeThickness = Const.Editor.NavBookmark.Thickness;
            l.Opacity = Const.Editor.NavBookmark.Opacity;
            canvasBookmarks.Children.Add(l);

            var txtBlock = CreateBookmarkLabel(b);
            canvasBookmarkLabels.Children.Add(txtBlock);
        }
    }
    internal void UndrawEditorNotes(List<Note> notes) {
        foreach (Note n in notes) {
            var nUid = Helper.UidGenerator(n);
            foreach (UIElement u in EditorGridNoteCanvas.Children) {
                if (u.Uid == nUid) {
                    EditorGridNoteCanvas.Children.Remove(u);
                    break;
                }
            }
        }
    }
    internal void UndrawEditorNotes(Note n) {
        UndrawEditorNotes(new List<Note>() { n });
    }
    internal void HighlightEditorNotes(List<Note> notes) {
        foreach (Note n in notes) {
            foreach (UIElement e in EditorGridNoteCanvas.Children) {
                if (e.Uid == Helper.UidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat, true);
                }
            }
        }
    }
    internal void HighlightEditorNotes(Note n) {
        HighlightEditorNotes(new List<Note>() { n });
    }
    internal void UnhighlightEditorNotes(List<Note> notes) {
        foreach (Note n in notes) {
            foreach (UIElement e in EditorGridNoteCanvas.Children) {
                if (e.Uid == Helper.UidGenerator(n)) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat);
                }
            }
        }
    }
    internal void UnhighlightEditorNotes(Note n) {
        UnhighlightEditorNotes(new List<Note>() { n });
    }
    internal void SetSongMouseOverLinePos(double newLinePos) {
        lineSongMouseover.Y1 = newLinePos;
        lineSongMouseover.Y2 = newLinePos;
    }
    internal void BeginDragSelection(Point mousePos) {
        if (editorIsDragging || (currentlyDraggingMarker != null && !isEditingMarker)) {
            return; 
        }
        imgPreviewNote.Visibility = Visibility.Hidden;
        editorDragSelectBorder.Visibility = Visibility.Visible;
        UpdateDragSelection(mousePos);
        editorIsDragging = true;
    }
    internal void EndDragSelection(Point mousePos, bool snapMouseMovements) {
        editorDragSelectBorder.Visibility = Visibility.Hidden;
        // calculate new selections
        List<Note> newSelection = new List<Note>();
        double startBeat = editorSelBeatStart;
        double endBeat = editorMouseBeatUnsnapped;
        int editorSelColEnd = editorMouseGridCol;
        if (editorSelColEnd == -1) {
            editorSelColEnd = mousePos.X < EditorGrid.ActualWidth / 2 ? 0 : 3;
        }
        foreach (Note n in mapEditor.currentMapDifficulty.notes) {
            // minor optimisation
            if (n.beat > Math.Max(startBeat, endBeat)) {
                break;
            }
            // check range
            if (Helper.DoubleRangeCheck(n.beat, startBeat, endBeat) && Helper.DoubleRangeCheck(n.col, editorSelColStart, editorSelColEnd)) {
                newSelection.Add(n);
            }
        }
        if (snapMouseMovements) {
            mapEditor.SelectNotes(newSelection);
        } else {
            mapEditor.SelectNewNotes(newSelection);
        }
    }
    internal void UpdateMousePosition(Point mousePos) {
        // calculate beat
        try {
            editorMouseBeatSnapped = BeatForPosition(mousePos.Y, true);
            editorMouseBeatUnsnapped = BeatForPosition(mousePos.Y, false);
        } catch {
            editorMouseBeatSnapped = 0;
            editorMouseBeatUnsnapped = 0;
        }

        // calculate column
        editorMouseGridCol = ColFromPos(mousePos.X);

        // set preview note visibility
        if (!editorIsDragging) {
            if (editorMouseGridCol < 0) {
                SetPreviewNoteVisibility(Visibility.Hidden);
            } else {
                SetPreviewNoteVisibility(Visibility.Visible);
            }
        }
    }
    internal void EditorGridMouseMove(Point mousePos, bool snapMouseMovements) {
        UpdateMousePosition(mousePos);

        double noteX = (1 + 4 * editorMouseGridCol) * unitSubLength;
        // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
        var unknownNoteXAdjustment = (unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2;

        double userOffsetBeat = gridOffset * mapEditor.globalBPM / 60;
        double userOffset = userOffsetBeat * unitLength;
        var adjustedMousePos = EditorGrid.ActualHeight - mousePos.Y - unitHeight / 2;
        double gridLength = unitLength / gridDivision;

        // place preview note   
        double previewNoteBottom = editorSnapToGrid ? (editorMouseBeatSnapped * gridLength * gridDivision + userOffset) : Math.Max(adjustedMousePos, userOffset);
        ImageSource previewNoteSource = RuneForBeat(userOffsetBeat + (editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped));
        double previewNoteLeft = noteX - unknownNoteXAdjustment + editorMarginGrid.Margin.Left;
        SetPreviewNote(previewNoteBottom, previewNoteLeft, previewNoteSource);

        // place preview line
        SetMouseoverLinePosition(mousePos.Y - markerDragOffset);

        

        // move markers if one is being dragged right now
        if (currentlyDraggingMarker != null && !isEditingMarker) {
            MoveMarker(mousePos, snapMouseMovements);
            parentWindow.Cursor = Cursors.Hand;
        // otherwise, update existing drag operations
        } else if (editorIsDragging) {
            UpdateDragSelection(mousePos);
        }
    }
    internal void EditorGridMouseUp(Point mousePos, bool snapMouseMovements) {
        if (editorMouseGridCol >= 0) {
            SetPreviewNoteVisibility(Visibility.Visible);
        }
        if (currentlyDraggingMarker != null && !isEditingMarker) {
            FinaliseMarkerEdit(mousePos, snapMouseMovements);
        } else if (editorIsDragging) {
            EndDragSelection(mousePos, snapMouseMovements);
        } else if (EditorGrid.IsMouseCaptured && editorMouseGridCol >= 0) {

            Note n = new Note(mouseBeat, editorMouseGridCol);

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
        editorIsDragging = false;
    }
    internal void EditorGridMouseDown(Point mousePos) {
        dragSelectStart = mousePos;
        editorSelBeatStart = unsnappedBeat;
        editorSelColStart = mouseColumn;
        EditorGrid.CaptureMouse();
    }
    internal void MoveMarker(Point mousePos, bool shiftKeyDown) {
        double newBottom = unitLength * BeatForPosition(mousePos.Y - markerDragOffset, shiftKeyDown);
        Canvas.SetBottom(currentlyDraggingMarker, newBottom + unitHeight / 2);
        SetMouseoverLineVisibility(Visibility.Visible);
    }
    internal void FinaliseMarkerEdit(Point mousePos, bool snapMouseMovements) {
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
    internal void EditBookmark(double beat) {
        mapEditor.RemoveBookmark(currentlyDraggingBookmark);
        currentlyDraggingBookmark.beat = beat;
        mapEditor.AddBookmark(currentlyDraggingBookmark);
        DrawGrid(false);
    }
    internal void PasteClipboardWithOffset() {
        mapEditor.PasteClipboard(editorMouseBeatSnapped);
    }
    internal void CreateBookmark() {
        double beat = BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, editorSnapToGrid);
        if (isMouseOnEditingGrid) {
            beat = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
        } else if (lineSongMouseover.Opacity > 0) {
            beat = mapEditor.globalBPM * parentWindow.songTotalTimeInSeconds / 60000 * (1 - lineSongMouseover.Y1 / borderNavWaveform.ActualHeight);
        }
        mapEditor.AddBookmark(new Bookmark(beat, Const.Editor.NavBookmark.DefaultName));
    }
    internal void CreateBPMChange(bool snappedToGrid) {
        double beat = (snappedToGrid) ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
        BPMChange previous = new BPMChange(0, mapEditor.globalBPM, gridDivision);
        foreach (var b in mapEditor.currentMapDifficulty.bpmChanges) {
            if (b.globalBeat < beat) {
                previous = b;
            }
        }
        mapEditor.AddBPMChange(new BPMChange(beat, previous.BPM, previous.gridDivision));
    }
    internal void CreateNote(int col, bool onMouse) {
        double mouseInput = editorSnapToGrid ? editorMouseBeatSnapped : editorMouseBeatUnsnapped;
        double defaultInput = BeatForPosition(scrollEditor.VerticalOffset + scrollEditor.ActualHeight - unitLengthUnscaled / 2, editorSnapToGrid);
        Note n = new Note(onMouse ? mouseInput: defaultInput, col);
        mapEditor.AddNotes(n);
    }
    private Line makeLine(double width, double offset) {
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
        txtBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(Const.Editor.NavBookmark.NameColour);

        txtBlock.Content = b.name;
        txtBlock.FontSize = Const.Editor.NavBookmark.NameSize;
        txtBlock.Padding = new Thickness(Const.Editor.NavBookmark.NamePadding);
        txtBlock.FontWeight = FontWeights.Bold;
        txtBlock.Opacity = Const.Editor.NavBookmark.Opacity;
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
            txtBox.FontSize = Const.Editor.NavBookmark.NameSize;
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
    private int ColFromPos(double pos) {
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
            int binarySearch = gridlines.BinarySearch(unsnapped);
            if (binarySearch > 0) {
                return gridlines[binarySearch];
            }
            int indx1 = -binarySearch - 1;
            int indx2 = Math.Max(0, indx1 - 1);
            snapped = (gridlines[indx1] - unsnapped) < (unsnapped - gridlines[indx2]) ? gridlines[indx1] : gridlines[indx2];
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
        Canvas.SetLeft(editorDragSelectBorder, p1.X);
        Canvas.SetTop(editorDragSelectBorder, p1.Y);
        editorDragSelectBorder.Width = delta.X;
        editorDragSelectBorder.Height = delta.Y;
    }
}
