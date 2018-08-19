/*
 * Based on https://www.cyotek.com/blog/dragging-items-in-a-listview-control-with-visual-insertion-guides
 */

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace mp3_rename
{
    public enum InsertionMode
    {
        BeforeItem,
        AfterItem
    }

    public partial class NinjaListView : ListView
    {
        public NinjaListView()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            this.InsertionLineColor = Color.Red;
            this.InsertionIndex = -1;
            this.IconView = false;
        }
        
        protected int InsertionIndex { get; set; }
        protected InsertionMode InsertionMode { get; set; }
        protected bool IconView { get; set; }
        protected bool IsDragInProgress { get; set; }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Red")]
        public virtual Color InsertionLineColor { get; set; }

        private const int WM_PAINT = 0xf;

        [DebuggerStepThrough]
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case WM_PAINT:
                    this.DrawInsertionLine();
                    break;
            }
        }

        private void DrawInsertionLine()
        {
            int index = this.InsertionIndex;
            if (index >= 0 && index < this.Items.Count)
            {
                Rectangle bounds = this.GetItemRect(index);
                if (this.IconView)
                {
                    DrawInsertionLineVertical(
                        (this.InsertionMode == InsertionMode.BeforeItem) ? bounds.Left : bounds.Right,
                        bounds.Top,
                        bounds.Height);
                }
                else
                {
                    DrawInsertionLineHorizon(
                        bounds.Left,
                        (this.InsertionMode == InsertionMode.BeforeItem) ? bounds.Top : bounds.Bottom,
                        bounds.Width);
                }
            }
        }

        private void DrawInsertionLineVertical(int x, int y1, int height)
        {
            using (Graphics g = this.CreateGraphics())
            {
                int arrowHeadSize = 7;
                int y2 = y1 + height;

                Point[] topArrowHead = new[]
                {
                    new Point(x - (arrowHeadSize / 2), y1),
                    new Point(x, y1 + arrowHeadSize),
                    new Point(x + (arrowHeadSize / 2), y1)
                };

                Point[] bottomArrowHead = new[]
                {
                    new Point(x - (arrowHeadSize / 2), y2),
                    new Point(x, y2 - arrowHeadSize),
                    new Point(x + (arrowHeadSize / 2), y2)
                };

                using (Pen pen = new Pen(this.InsertionLineColor))
                {
                    g.DrawLine(pen, x, y1, x, y2 - 1);
                }

                using (Brush brush = new SolidBrush(this.InsertionLineColor))
                {
                    g.FillPolygon(brush, topArrowHead);
                    g.FillPolygon(brush, bottomArrowHead);
                }
            }
        }

        private void DrawInsertionLineHorizon(int x1, int y, int width)
        {
            using (Graphics g = this.CreateGraphics())
            {
                int arrowHeadSize = 7;
                int x2 = x1 + width;

                Point[] leftArrowHead = new[] 
                {
                    new Point(x1, y - (arrowHeadSize / 2)),
                    new Point(x1 + arrowHeadSize, y),
                    new Point(x1, y + (arrowHeadSize / 2))
                };

                Point[] rightArrowHead = new[] 
                {
                    new Point(x2, y - (arrowHeadSize / 2)),
                    new Point(x2 - arrowHeadSize, y),
                    new Point(x2, y + (arrowHeadSize / 2))
                };

                using (Pen pen = new Pen(this.InsertionLineColor))
                {
                    g.DrawLine(pen, x1, y, x2 - 1, y);
                }

                using (Brush brush = new SolidBrush(this.InsertionLineColor))
                {
                    g.FillPolygon(brush, leftArrowHead);
                    g.FillPolygon(brush, rightArrowHead);
                }
            }
        }

        protected override void OnItemDrag(ItemDragEventArgs e)
        {
            if (this.SelectedItems.Count > 0)
            {
                this.IconView = 
                    this.View == View.LargeIcon ||
                    this.View == View.SmallIcon ||
                    this.View == View.Tile;

                this.IsDragInProgress = true;
                this.DoDragDrop(this.SelectedItems, DragDropEffects.Move);
            }

            base.OnItemDrag(e);
        }

        protected override void OnDragOver(DragEventArgs dragEvent)
        {
            if (this.IsDragInProgress)
            {
                int insertionIndex;
                InsertionMode insertionMode;

                Point clientPoint = this.PointToClient(new Point(dragEvent.X, dragEvent.Y));
                ListViewItem dropItem = this.GetItemAt(clientPoint.X, clientPoint.Y);

                if (dropItem != null)
                {
                    Rectangle bounds = dropItem.GetBounds(ItemBoundsPortion.Entire);
                    insertionIndex = dropItem.Index;
                    bool inserrBefore = this.IconView
                        ? (clientPoint.X < bounds.Left + bounds.Width / 2)
                        : (clientPoint.Y < bounds.Top + (bounds.Height / 2));
                    insertionMode = inserrBefore ? InsertionMode.BeforeItem : InsertionMode.AfterItem;

                    dragEvent.Effect = DragDropEffects.Move;
                }
                else
                {
                    insertionIndex = -1;
                    insertionMode = this.InsertionMode;

                    dragEvent.Effect = DragDropEffects.None;
                }

                if (insertionIndex != this.InsertionIndex || insertionMode != this.InsertionMode)
                {
                    this.InsertionMode = insertionMode;
                    this.InsertionIndex = insertionIndex;
                    this.Invalidate();
                }
            }

            base.OnDragOver(dragEvent);
        }

        protected override void OnDragLeave(System.EventArgs e)
        {
            this.InsertionIndex = -1;
            this.Invalidate();

            base.OnDragLeave(e);
        }

        protected override void OnDragDrop(DragEventArgs dragEvent)
        {
            int selectedCount = this.SelectedItems.Count;

            if (this.IsDragInProgress && selectedCount > 0 && this.InsertionIndex != -1)
            {
                int dropIndex = this.InsertionIndex;
                if (this.InsertionMode == InsertionMode.AfterItem)
                {
                    dropIndex ++;
                }

                ListViewItem[] sel = new ListViewItem[selectedCount];
                this.SelectedItems.CopyTo(sel, 0);

                for (int i = 0; i < selectedCount; i++)
                {
                    //
                    // ListViewItems: * = selected, ^ = insertion indicator
                    //
                    // Original:   0  1* 2  3* 4* 5  ^  6  7* 8*
                    // Change to:  0  2  5  ^  1* 3* 4* 7* 8* 6
                    //
                    ListViewItem dragItem = sel[i];
                    int itemIndex = dropIndex;
                    if (dragItem.Index >= dropIndex)
                    {
                        dropIndex ++;
                    }

                    bool focused = dragItem.Focused;

                    // Move the item to the correct location
                    ListViewItem insertItem = (ListViewItem)dragItem.Clone();
                    this.Items.Insert(itemIndex, insertItem);
                    this.Items.Remove(dragItem);

                    insertItem.Selected = true;
                    insertItem.Focused = focused;
                }
                
                this.InsertionIndex = -1;
                this.IsDragInProgress = false;

                //
                // https://stackoverflow.com/questions/32299729/listview-item-not-moving
                // Force ListView to update its content and layout them as expected
                //
                if (this.IconView)
                {
                    this.Alignment = ListViewAlignment.Default;
                    this.Alignment = ListViewAlignment.Top;
                }

                this.Invalidate();
            }

            base.OnDragDrop(dragEvent);
        }
    }    
}
