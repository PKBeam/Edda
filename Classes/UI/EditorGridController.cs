﻿using Edda;
using Edda.Classes.MapEditorNS;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Const;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using DrawingColor = System.Drawing.Color;
using Image = System.Windows.Controls.Image;
using MediaColor = System.Windows.Media.Color;
using Point = System.Windows.Point;

public class EditorGridController : IDisposable {

    MapEditor mapEditor;

    // constructor variables
    MainWindow parentWindow;
    Canvas EditorGrid;
    ScrollViewer scrollEditor;
    ColumnDefinition referenceCol;
    RowDefinition referenceRow;
    Border borderNavWaveform;
    ColumnDefinition colWaveformVertical;
    Image imgWaveformVertical;
    ScrollViewer scrollSpectrogram;
    StackPanel panelSpectrogram;
    Canvas canvasSpectrogramLowerOffset;
    Canvas canvasSpectrogramUpperOffset;
    Image[] imgSpectrogramChunks;
    Grid editorMarginGrid;
    Canvas canvasNavInputBox;
    Canvas canvasBookmarks;
    Canvas canvasBookmarkLabels;
    Line lineSongMouseover;

    // dispatcher of the main application UI thread
    Dispatcher dispatcher;

    // user-defined settings 
    public double gridSpacing;
    public int gridDivision;
    public bool showWaveform;
    public bool snapToGrid = true;
    // spectrogram settings
    public bool? showSpectrogram = null;
    public bool spectrogramCache = true;
    public VorbisSpectrogramGenerator.SpectrogramType spectrogramType = VorbisSpectrogramGenerator.SpectrogramType.Standard;
    public VorbisSpectrogramGenerator.SpectrogramQuality spectrogramQuality = VorbisSpectrogramGenerator.SpectrogramQuality.Medium;
    public int spectrogramFrequency = Editor.Spectrogram.DefaultFreq;
    public string spectrogramColormap = Spectrogram.Colormap.Blues.Name;
    public bool spectrogramFlipped = false;
    public bool? spectrogramChunking = null;

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
    VorbisSpectrogramGenerator audioSpectrogram;
    VorbisWaveformGenerator audioWaveform;
    VorbisWaveformGenerator navWaveform;

    // marker editing
    bool isEditingMarker = false;
    double markerDragOffset = 0;
    Canvas currentlyDraggingMarker;
    Bookmark currentlyDraggingBookmark;
    BPMChange currentlyDraggingBPMChange;

    // dragging variables
    bool isDragging = false;
    Point dragSelectStart;

    // other
    public bool isMapDifficultySelected {
        get {
            return mapEditor?.currentMapDifficulty != null;
        }
    }
    public int currentMapDifficultyIndex {
        get {
            return mapEditor.currentDifficultyIndex;
        }
    }
    public SortedSet<Note> currentMapDifficultyNotes {
        get {
            return mapEditor?.currentMapDifficulty?.notes;
        }
    }
    public SortedSet<BPMChange> currentMapDifficultyBpmChanges {
        get {
            return mapEditor.currentMapDifficulty.bpmChanges;
        }
    }

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
    public EditorGridController(
        MainWindow parentWindow,
        Canvas EditorGrid,
        ScrollViewer scrollEditor,
        ColumnDefinition referenceCol,
        RowDefinition referenceRow,
        Border borderNavWaveform,
        ColumnDefinition colWaveformVertical,
        Image imgWaveformVertical,
        ScrollViewer scrollSpectrogram,
        StackPanel panelSpectrogram,
        Grid editorMarginGrid,
        Canvas canvasNavInputBox,
        Canvas canvasBookmarks,
        Canvas canvasBookmarkLabels,
        Line lineSongMouseover
    ) {
        this.parentWindow = parentWindow;
        this.EditorGrid = EditorGrid;
        this.referenceCol = referenceCol;
        this.referenceRow = referenceRow;
        this.scrollEditor = scrollEditor;
        this.borderNavWaveform = borderNavWaveform;
        this.colWaveformVertical = colWaveformVertical;
        this.imgWaveformVertical = imgWaveformVertical;
        this.scrollSpectrogram = scrollSpectrogram;
        this.panelSpectrogram = panelSpectrogram;
        this.editorMarginGrid = editorMarginGrid;
        this.canvasNavInputBox = canvasNavInputBox;
        this.canvasBookmarks = canvasBookmarks;
        this.canvasBookmarkLabels = canvasBookmarkLabels;
        this.lineSongMouseover = lineSongMouseover;

        dispatcher = parentWindow.Dispatcher;

        imgWaveformVertical.Opacity = Editor.NavWaveformOpacity;
        imgWaveformVertical.Stretch = Stretch.Fill;

        SetupSpectrogramContent();

        lineSongMouseover.Opacity = 0;

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

    public void Dispose() {
        // Clear the most memory-heavy components
        noteCanvas.Children.Clear();
        EditorGrid.Children.Clear();
        panelSpectrogram.Children.Clear();
        imgWaveformVertical.Source = null;
        imgAudioWaveform.Source = null;
        foreach (var imgSpectorgramChunk in imgSpectrogramChunks) {
            imgSpectorgramChunk.Source = null;
        }

        // Unbind references
        mapEditor = null;
        parentWindow = null;
        EditorGrid = null;
        scrollEditor = null;
        referenceCol = null;
        referenceRow = null;
        borderNavWaveform = null;
        colWaveformVertical = null;
        imgWaveformVertical = null;
        scrollSpectrogram = null;
        panelSpectrogram = null;
        canvasSpectrogramLowerOffset = null;
        canvasSpectrogramUpperOffset = null;
        imgSpectrogramChunks = null;
        editorMarginGrid = null;
        canvasNavInputBox = null;
        canvasBookmarks = null;
        canvasBookmarkLabels = null;
        lineSongMouseover = null;
        dispatcher = null;
        dragSelectBorder = null;
        lineGridMouseover = null;
        noteCanvas = null;
        imgAudioWaveform = null;
        imgPreviewNote = null;

        audioSpectrogram?.Dispose();
        audioSpectrogram = null;
        audioWaveform?.Dispose();
        audioWaveform = null;
        navWaveform?.Dispose();
        navWaveform = null;

        currentlyDraggingMarker = null;
        currentlyDraggingBookmark = null;
        currentlyDraggingBPMChange = null;
    }

    public void InitMap(MapEditor me) {
        this.mapEditor = me;
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
        audioSpectrogram = new VorbisSpectrogramGenerator(songPath, spectrogramCache, spectrogramType, spectrogramQuality, spectrogramFrequency, spectrogramColormap, spectrogramFlipped);
        audioWaveform = new VorbisWaveformGenerator(songPath);
        navWaveform = new VorbisWaveformGenerator(songPath);
    }
    public void RefreshSpectrogramWaveform() {
        audioSpectrogram?.InitSettings(spectrogramCache, spectrogramType, spectrogramQuality, spectrogramFrequency, spectrogramColormap, spectrogramFlipped);
    }
    public void DrawScrollingWaveforms() {
        if (showWaveform) {
            DrawMainWaveform();
        }
        if (showSpectrogram == true) {
            DrawSpectrogram();
        }
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
    public void SetupSpectrogramContent() {
        panelSpectrogram.Children.Clear();
        // Upper offset
        canvasSpectrogramUpperOffset = new Canvas();
        canvasSpectrogramUpperOffset.SnapsToDevicePixels = true;
        panelSpectrogram.Children.Add(canvasSpectrogramUpperOffset);
        // Image chunks - inserted in inverse order
        var numChunks = spectrogramChunking.HasValue ? (spectrogramChunking.Value ? Editor.Spectrogram.NumberOfChunks : 1) : 0;
        imgSpectrogramChunks = new Image[numChunks];
        for (int i = 0; i < numChunks; ++i) {
            Image imgChunk = new Image();
            imgChunk.Stretch = Stretch.Fill;
            imgChunk.SnapsToDevicePixels = true;
            imgSpectrogramChunks[i] = imgChunk;
            panelSpectrogram.Children.Insert(1, imgChunk);
        }
        // Lower offset
        canvasSpectrogramLowerOffset = new Canvas();
        canvasSpectrogramLowerOffset.SnapsToDevicePixels = true;
        panelSpectrogram.Children.Add(canvasSpectrogramLowerOffset);
    }
    private void ResizeSpectrogram() {
        // Upper offset
        canvasSpectrogramUpperOffset.Height = scrollEditor.ActualHeight - (unitHeight / 2);
        // Image chunks
        var numChunks = imgSpectrogramChunks.Length;
        for (int i = 0; i < numChunks; ++i) {
            Image imgChunk = imgSpectrogramChunks[i];
            imgChunk.Width = scrollSpectrogram.ActualWidth;
            imgChunk.Height = (EditorGrid.Height - scrollEditor.ActualHeight) / (double)numChunks;
        }
        // Lower offset
        canvasSpectrogramLowerOffset.Height = unitHeight / 2;
    }
    internal void DrawSpectrogram() {
        ResizeSpectrogram();
        CreateSpectrogram();
    }
    private void CreateSpectrogram() {
        Task.Run(() => {
            DateTime before = DateTime.Now;
            var numChunks = EditorGrid.ActualHeight == 0 || scrollSpectrogram.ActualWidth == 0 ? 0 : imgSpectrogramChunks.Length;
            ImageSource[] bmps = audioSpectrogram.Draw(numChunks);
            Trace.WriteLine($"INFO: Drew spectrogram in {(DateTime.Now - before).TotalSeconds} sec");

            if (bmps != null && bmps.Length == numChunks) {
                this.dispatcher.Invoke(() => {
                    for (int i = 0; i < numChunks; ++i) {
                        if (bmps != null) {
                            imgSpectrogramChunks[i].Source = bmps[i];
                        }
                    }
                    DrawingColor bgColor = audioSpectrogram.GetBackgroundColor();
                    var spectrogramBackgroundBrush = new SolidColorBrush(MediaColor.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
                    canvasSpectrogramLowerOffset.Background = spectrogramBackgroundBrush;
                    canvasSpectrogramUpperOffset.Background = spectrogramBackgroundBrush;
                });
            }
        });
    }

    // grid drawing
    public void UpdateGridHeight() {
        // resize editor grid height to fit scrollEditor height
        if (parentWindow.songTotalTimeInSeconds.HasValue) {
            double beats = mapEditor.GlobalBPM / 60 * parentWindow.songTotalTimeInSeconds.Value;
            EditorGrid.Height = beats * unitLength + scrollEditor.ActualHeight;
        }
    }
    public void DrawGrid(bool redrawWaveform = true) {
        UpdateGridHeight();

        EditorGrid.Children.Clear();

        DateTime start = DateTime.Now;

        // draw gridlines
        EditorGrid.Children.Add(lineGridMouseover);
        DrawGridLines(EditorGrid.Height - scrollEditor.ActualHeight);

        // end of song marker
        var l = MakeLine(EditorGrid.ActualWidth, 0);
        Canvas.SetBottom(l, mapEditor.GlobalBPM / 60 * mapEditor.SongDuration * unitLength + unitHeight / 2);
        l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.MajorGridlineColour);
        l.StrokeThickness = Editor.MajorGridlineThickness;
        EditorGrid.Children.Add(l);

        // then draw the waveform
        if (showWaveform) {
            EditorGrid.Children.Add(imgAudioWaveform);
        }
        if (redrawWaveform && EditorGrid.Height - scrollEditor.ActualHeight > 0) {
            DrawScrollingWaveforms();
        }

        // then draw the notes
        noteCanvas.Children.Clear();
        DrawNotes(mapEditor.currentMapDifficulty.notes);
        HighlightNotes(mapEditor.currentMapDifficulty.selectedNotes);

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

        // the position to place gridlines
        var offset = 0.0;

        var localBPM = mapEditor.GlobalBPM;
        var localGridDiv = gridDivision;

        // draw gridlines
        int counter = 0;
        var bpmChangesEnumerator = mapEditor.currentMapDifficulty.bpmChanges.GetEnumerator();
        var hasNextBpmChange = bpmChangesEnumerator.MoveNext();
        while (offset <= gridHeight) {

            // add new gridline
            bool isMajor = counter % localGridDiv == 0;
            var l = makeGridLine(offset, isMajor);
            EditorGrid.Children.Add(l);
            if (isMajor) {
                majorGridBeatLines.Add((offset) / unitLength);
            }
            gridBeatLines.Add((offset) / unitLength);

            offset += mapEditor.GlobalBPM / localBPM * unitLength / localGridDiv;
            counter++;

            // check for BPM change
            if (hasNextBpmChange && Helper.DoubleApproxGreaterEqual((offset) / unitLength, bpmChangesEnumerator.Current.globalBeat)) {
                BPMChange next = bpmChangesEnumerator.Current;

                offset = next.globalBeat * unitLength;
                localBPM = next.BPM;
                localGridDiv = next.gridDivision;

                hasNextBpmChange = bpmChangesEnumerator.MoveNext();
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

            txtBlock.PreviewMouseLeftButtonDown += new MouseButtonEventHandler((src, e) => {
                if (parentWindow.ctrlKeyDown) {
                    mapEditor.SelectNotesInBookmark(b);
                } else {
                    currentlyDraggingMarker = bookmarkCanvas;
                    currentlyDraggingBookmark = b;
                    currentlyDraggingBPMChange = null;
                    markerDragOffset = e.GetPosition(bookmarkCanvas).Y;
                    SetPreviewNoteVisibility(Visibility.Hidden);
                    EditorGrid.CaptureMouse();
                }
                e.Handled = true;
            });
            txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
                if (!(e.ChangedButton == MouseButton.Middle)) {
                    return;
                }
                var res = MessageBox.Show(parentWindow, "Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
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
        BPMChange prev = new BPMChange(0, mapEditor.GlobalBPM, gridDivision);
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
                if (parentWindow.ctrlKeyDown) {
                    mapEditor.SelectNotesInBPMChange(b);
                } else {
                    currentlyDraggingMarker = bpmChangeCanvas;
                    currentlyDraggingBPMChange = b;
                    currentlyDraggingBookmark = null;
                    markerDragOffset = e.GetPosition(bpmChangeCanvas).Y;
                    SetPreviewNoteVisibility(Visibility.Hidden);
                    EditorGrid.CaptureMouse();
                }
                e.Handled = true;
            });
            bpmChangeFlagCanvas.PreviewMouseDown += new MouseButtonEventHandler((src, e) => {
                if (!(e.ChangedButton == MouseButton.Middle)) {
                    return;
                }
                var res = MessageBox.Show(parentWindow, "Are you sure you want to delete this timing change?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
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
        if (!parentWindow.songTotalTimeInSeconds.HasValue || mapEditor.currentMapDifficulty == null) {
            return;
        }
        canvasBookmarks.Children.Clear();
        canvasBookmarkLabels.Children.Clear();
        foreach (Bookmark b in mapEditor.currentMapDifficulty.bookmarks) {
            var l = MakeLine(borderNavWaveform.ActualWidth, borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (mapEditor.GlobalBPM * parentWindow.songTotalTimeInSeconds.Value * 1000)));
            l.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(Editor.NavBookmark.Colour);
            l.StrokeThickness = Editor.NavBookmark.Thickness;
            l.Opacity = Editor.NavBookmark.Opacity;
            canvasBookmarks.Children.Add(l);

            var txtBlock = CreateBookmarkLabel(b);
            canvasBookmarkLabels.Children.Add(txtBlock);
        }
    }

    // note drawing
    internal void DrawNotes(IEnumerable<Note> notes) {
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
    internal void UndrawNotes(IEnumerable<Note> notes) {
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
    internal void HighlightNotes(IEnumerable<Note> notes) {
        foreach (Note n in notes) {
            var noteUid = Helper.UidGenerator(n);
            foreach (UIElement e in noteCanvas.Children) {
                if (e.Uid == noteUid) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat, true);
                    break; // UID is unique
                }
            }
        }
    }
    internal void HighlightNotes(Note n) {
        HighlightNotes(new List<Note>() { n });
    }
    internal void HighlightAllNotes() {
        foreach (UIElement e in noteCanvas.Children) {
            if (e is not Image img) continue;
            var n = Helper.NoteFromUid(e.Uid);
            if (n == null) continue;
            img.Source = RuneForBeat(n.beat, true);
        }
    }
    internal void UnhighlightNotes(IEnumerable<Note> notes) {
        foreach (Note n in notes) {
            var noteUid = Helper.UidGenerator(n);
            foreach (UIElement e in noteCanvas.Children) {
                if (e.Uid == noteUid) {
                    var img = (Image)e;
                    img.Source = RuneForBeat(n.beat);
                    break; // UID is unique
                }
            }
        }
    }
    internal void UnhighlightNotes(Note n) {
        UnhighlightNotes(new List<Note>() { n });
    }
    internal void UnhighlightAllNotes() {
        foreach (UIElement e in noteCanvas.Children) {
            if (e is not Image img) continue;
            var n = Helper.NoteFromUid(e.Uid);
            if (n == null) continue;
            img.Source = RuneForBeat(n.beat);
        }
    }
    internal List<double> GetBeats() {
        return majorGridBeatLines;
    }

    // mouse input handling
    internal void GridMouseMove(Point mousePos) {
        // check if mouse is out of bounds of the song map
        mouseOutOfBounds = mousePos.Y < scrollEditor.ActualHeight - unitHeight / 2;

        UpdateMousePosition(mousePos);

        double noteX = (1 + 4 * mouseGridCol) * unitSubLength;
        // for some reason Canvas.SetLeft(0) doesn't correspond to the leftmost of the canvas, so we need to do some unknown adjustment to line it up
        var unknownNoteXAdjustment = (unitLength / unitLengthUnscaled - 1) * unitLengthUnscaled / 2;

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
            double previewNoteBottom = snapToGrid ? (mouseBeatSnapped * gridLength * gridDivision) : Math.Max(adjustedMousePos, 0);
            ImageSource previewNoteSource = RuneForBeat((snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped));
            double previewNoteLeft = noteX - unknownNoteXAdjustment + editorMarginGrid.Margin.Left;
            SetPreviewNote(previewNoteBottom, previewNoteLeft, previewNoteSource);

            // place preview line
            SetMouseoverLinePosition(mousePos.Y - markerDragOffset);
        }

        // move markers if one is being dragged right now
        if (!mouseOutOfBounds && currentlyDraggingMarker != null && !isEditingMarker) {
            MoveMarker(mousePos);
            parentWindow.Cursor = Cursors.Hand;
            // otherwise, update existing drag operations
        } else if (isDragging) {
            UpdateDragSelection(mousePos);
        }
    }
    internal void GridMouseUp(Point mousePos) {

        if (mouseGridCol >= 0 && mouseGridCol < 4) {
            SetPreviewNoteVisibility(Visibility.Visible);
        }
        if (currentlyDraggingMarker != null && !isEditingMarker) {
            var markerPos = mousePos;
            if (mouseOutOfBounds) {
                markerPos.Y = scrollEditor.ActualHeight - unitHeight / 2;
            }
            FinaliseMarkerEdit(markerPos);
        } else if (isDragging) {
            EndDragSelection(mousePos);
        } else if (!mouseOutOfBounds && EditorGrid.IsMouseCaptured && mouseGridCol >= 0 && mouseGridCol < 4) {

            Note n = new Note(mouseBeat, mouseGridCol);

            // select the note if it exists
            if (mapEditor.currentMapDifficulty.notes.Contains(n)) {
                if (parentWindow.shiftKeyDown) {
                    mapEditor.ToggleSelection(n);
                } else {
                    mapEditor.SelectNewNotes(n);
                }
                // otherwise create and add it
            } else {
                mapEditor.AddNotes(n);
                parentWindow.drummer?.Play(n.col);
            }
        }

        EditorGrid.ReleaseMouseCapture();
        isDragging = false;
    }
    internal void GridRightMouseUp() {
        // remove the note
        Note n = mouseNote;
        mapEditor.RemoveNote(n);
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
    internal void EndDragSelection(Point mousePos) {
        dragSelectBorder.Visibility = Visibility.Hidden;
        // calculate new selections
        double startBeat = BeatForPosition(dragSelectStart.Y, false);
        double endBeat = mouseBeatUnsnapped;
        if (Helper.DoubleApproxGreater(startBeat, endBeat)) {
            (startBeat, endBeat) = (endBeat, startBeat);
        }
        int startCol = ColForPosition(dragSelectStart.X);
        int endCol = mouseGridCol;
        if (startCol > endCol) {
            (startCol, endCol) = (endCol, startCol);
        }
        var newSelection =
            mapEditor.currentMapDifficulty
                .GetNotesRange(startBeat, endBeat)
                .Where(n => n.col >= Math.Max(startCol, 0) && n.col <= Math.Min(endCol, 3));
        if (parentWindow.shiftKeyDown) {
            mapEditor.SelectNotes(newSelection);
        } else {
            mapEditor.SelectNewNotes(newSelection);
        }
    }
    internal void MoveMarker(Point mousePos) {
        double newBottom = unitLength * BeatForPosition(mousePos.Y - markerDragOffset, parentWindow.shiftKeyDown);
        Canvas.SetBottom(currentlyDraggingMarker, newBottom + unitHeight / 2);
        SetMouseoverLineVisibility(Visibility.Visible);
    }
    private void FinaliseMarkerEdit(Point mousePos) {
        if (currentlyDraggingBPMChange == null) {
            EditBookmark(BeatForPosition(mousePos.Y - markerDragOffset, parentWindow.shiftKeyDown));
        } else {
            mapEditor.RemoveBPMChange(currentlyDraggingBPMChange, false);
            currentlyDraggingBPMChange.globalBeat = BeatForPosition(mousePos.Y - markerDragOffset, parentWindow.shiftKeyDown);
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
    internal void PasteClipboardWithOffset(bool onMouseColumn) {
        mapEditor.PasteClipboard(mouseBeatSnapped, onMouseColumn ? mouseColumn : null);
    }
    internal void CreateBookmark(bool onMouse = true) {
        double beat = currentSeekBeat;
        if (onMouse) {
            if (isMouseOnEditingGrid) {
                beat = snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped;
                // add bookmark on nav waveform
            } else if (lineSongMouseover.Opacity > 0 && parentWindow.songTotalTimeInSeconds.HasValue) {
                beat = mapEditor.GlobalBPM * parentWindow.songTotalTimeInSeconds.Value / 60000 * (1 - lineSongMouseover.Y1 / borderNavWaveform.ActualHeight);
            }
        }
        mapEditor.AddBookmark(new Bookmark(beat, Editor.NavBookmark.DefaultName));
    }
    internal void CreateBPMChange(bool snappedToGrid, bool onMouse = true) {
        double beat = (snappedToGrid) ? mouseBeatSnapped : mouseBeatUnsnapped;
        if (!onMouse) {
            beat = currentSeekBeat;
        }
        BPMChange previous = new BPMChange(0, mapEditor.GlobalBPM, gridDivision);
        foreach (var b in mapEditor.currentMapDifficulty.bpmChanges) {
            if (b.globalBeat < beat) {
                previous = b;
            }
        }
        mapEditor.AddBPMChange(new BPMChange(beat, previous.BPM, previous.gridDivision));
    }
    internal void AddNoteAt(int col, bool onMouse) {
        double mouseInput = snapToGrid ? mouseBeatSnapped : mouseBeatUnsnapped;
        Note n = new Note(onMouse ? mouseInput : currentSeekBeat, col);
        mapEditor.AddNotes(n);
    }
    internal void ShiftSelectionByRow(MoveNote direction) {
        mapEditor.ShiftSelectionByBeat(direction);
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
        var lastBPMChange = mapEditor.GetLastBeatChange(beat);
        double beatNormalised = beat - lastBPMChange.globalBeat;
        beatNormalised /= mapEditor.GetGridLength(lastBPMChange.BPM, 1);
        return Helper.BitmapImageForBeat(beatNormalised, highlight);
    }
    private Label CreateBookmarkLabel(Bookmark b) {
        var offset = borderNavWaveform.ActualHeight * (1 - 60000 * b.beat / (mapEditor.GlobalBPM * parentWindow.songTotalTimeInSeconds.Value * 1000));
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
            if (parentWindow.ctrlKeyDown) {
                mapEditor.SelectNotesInBookmark(b);
            } else {
                parentWindow.songSeekPosition = b.beat / mapEditor.GlobalBPM * 60000;
                parentWindow.navMouseDown = false;
            }
            e.Handled = true;
        });
        txtBlock.MouseDown += new MouseButtonEventHandler((src, e) => {
            if (!(e.ChangedButton == MouseButton.Middle)) {
                return;
            }
            var res = MessageBox.Show(parentWindow, "Are you sure you want to delete this bookmark?", "Confirm Deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
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
        var pos = EditorGrid.ActualHeight - position - unitHeight / 2;
        double gridLength = unitLength / gridDivision;
        // check if mouse position would correspond to a negative row index
        double snapped = 0;
        double unsnapped = 0;
        if (pos >= 0) {
            unsnapped = pos / unitLength;
            int binarySearch = gridBeatLines.BinarySearch(unsnapped);
            if (binarySearch > 0) {
                return gridBeatLines[binarySearch];
            }
            int indx1 = Math.Min(gridBeatLines.Count - 1, -binarySearch - 1);
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
    private double BeatForRow(double row) {
        return row / (double)gridDivision;
    }
}