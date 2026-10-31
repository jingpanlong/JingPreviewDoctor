using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace JingPreview {
partial class MainForm {
 readonly List<FlowLayoutPanel> adaptiveBars=new List<FlowLayoutPanel>();
 readonly List<Label> adaptiveLabels=new List<Label>();
 readonly Dictionary<TabPage,Panel> layoutBodies=new Dictionary<TabPage,Panel>();
 readonly Dictionary<DataGridView,int[]> gridMinimums=new Dictionary<DataGridView,int[]>();
 bool refreshingLayout,closingLayout;
 TextBox formatSearch;
 TextBox batchDetails;
 void ApplyFormatFilter(){if(formatSearch==null)return;grid.CurrentCell=null;foreach(DataGridViewRow row in grid.Rows)row.Visible=String.Join(" ",new[]{Convert.ToString(row.Cells[0].Value),Convert.ToString(row.Cells[1].Value)}).IndexOf(formatSearch.Text,StringComparison.OrdinalIgnoreCase)>=0;}
 internal bool IsClosingLayout {get{return closingLayout;}}
 void ShutdownLayoutQuality(){closingLayout=true;}
 void InitLayoutQuality(){
  tabs.Multiline=true;
  ConfigureLayoutTree(this);
  ConfigureGrid(grid,new[]{90,330,260,260});
  ConfigureGrid(batchGrid,new[]{180,90,100,120,300,320});
  ConfigureGrid(backupGrid,new[]{360,190,130});
  foreach(TabPage page in tabs.TabPages){
   Control[] children=new Control[page.Controls.Count];page.Controls.CopyTo(children,0);
   Panel body=new Panel{Dock=DockStyle.Top,Height=page.ClientSize.Height,Padding=Padding.Empty};
   page.Controls.Clear();body.Controls.AddRange(children);page.Controls.Add(body);page.AutoScroll=true;
   layoutBodies[page]=body;body.Layout+=delegate{RefreshAdaptiveLayout();};
  }
  batchDetails=new TextBox{Dock=DockStyle.Bottom,Height=100,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Text="选中一行可在这里查看和复制完整路径、输出位置与错误信息。"};
  batchGrid.Parent.Controls.Add(batchDetails);batchGrid.SelectionChanged+=delegate{UpdateBatchDetails();};
  // The window header and footer also grow when system fonts require another line.
  foreach(Control c in Controls){Panel head=c as Panel;if(head!=null&&head.Dock==DockStyle.Top){Control title=null;foreach(Control label in head.Controls){label.Dock=DockStyle.Top;if(label.Text.Contains("PREVIEW DOCTOR"))title=label;Label item=label as Label;if(item!=null)adaptiveLabels.Add(item);}if(title!=null)head.Controls.SetChildIndex(title,head.Controls.Count-1);}Label footer=c as Label;if(footer!=null&&footer.Dock==DockStyle.Bottom){footer.Text="微信公众号：景同志   |   小红书：景同学\r\n本地运行 · 设置可恢复";adaptiveLabels.Add(footer);}}
  tabs.SelectedIndexChanged+=delegate{RefreshAdaptiveLayout();if(IsHandleCreated&&!IsDisposed&&!Disposing&&!closingLayout)BeginInvoke(new Action(RefreshAdaptiveLayout));};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!e.Cancel)ShutdownLayoutQuality();};
  Shown+=delegate{RefreshAdaptiveLayout();};
  ResizeEnd+=delegate{RefreshAdaptiveLayout();};
 }
 void ConfigureLayoutTree(Control parent){
  Control[] controls=new Control[parent.Controls.Count];parent.Controls.CopyTo(controls,0);
  foreach(Control c in controls){
   FlowLayoutPanel flow=c as FlowLayoutPanel;
   if(flow!=null && flow.Dock!=DockStyle.None){
    flow.AutoScroll=false;flow.WrapContents=true;flow.AutoSize=true;flow.AutoSizeMode=AutoSizeMode.GrowAndShrink;
    flow.Padding=new Padding(4,6,4,6);adaptiveBars.Add(flow);
   }
   Button button=c as Button;if(button!=null){button.AutoSize=true;button.AutoSizeMode=AutoSizeMode.GrowAndShrink;button.Padding=new Padding(8,3,8,3);button.Margin=new Padding(4);}
   CheckBox check=c as CheckBox;if(check!=null)check.AutoSize=true;
   Label label=c as Label;
   if(label!=null && (label.Dock==DockStyle.Top||label.Dock==DockStyle.Bottom)){
    if(label.Parent is TabPage){adaptiveLabels.Add(label);label.SizeChanged+=delegate{FitLabel(label);};label.TextChanged+=delegate{FitLabel(label);};}
   }
   ConfigureLayoutTree(c);
  }
 }
 void FitLabel(Label label){if(label.IsDisposed||label.Disposing||closingLayout)return;int width=label.Parent==null?label.Width:Math.Max(40,label.Parent.ClientSize.Width-label.Parent.Padding.Horizontal);Size size=label.GetPreferredSize(new Size(width,Int32.MaxValue));int required=Math.Max(size.Height,label.Font.Height+label.Padding.Vertical)+6;Size target=new Size(width,required);if(label.Size!=target)label.Size=target;}
 void ConfigureGrid(DataGridView view,int[] widths){
  gridMinimums[view]=widths;
  view.EnableHeadersVisualStyles=false;
  view.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{Font=new Font(Font,FontStyle.Bold),BackColor=Color.FromArgb(234,240,248),ForeColor=ink,Padding=new Padding(8,7,8,7),Alignment=DataGridViewContentAlignment.MiddleLeft,WrapMode=DataGridViewTriState.True};
  view.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
  view.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  view.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
  view.DefaultCellStyle.Padding=new Padding(6,5,6,5);
  view.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
  view.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;
  view.RowTemplate.MinimumHeight=Font.Height+14;
  view.AllowUserToResizeRows=true;view.ScrollBars=ScrollBars.Both;
  for(int i=0;i<view.Columns.Count;i++){
   view.Columns[i].MinimumWidth=widths[i];view.Columns[i].FillWeight=widths[i];
   view.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable;
  }
  view.FontChanged+=delegate{Font previous=view.ColumnHeadersDefaultCellStyle.Font;view.ColumnHeadersDefaultCellStyle.Font=new Font(view.Font,FontStyle.Bold);if(previous!=null)previous.Dispose();MeasureGridColumns(view);view.AutoResizeColumnHeadersHeight();};
  view.DataBindingComplete+=delegate{view.AutoResizeColumnHeadersHeight();};
  view.CellToolTipTextNeeded+=delegate(object sender,DataGridViewCellToolTipTextNeededEventArgs e){if(e.RowIndex>=0&&e.ColumnIndex>=0)e.ToolTipText=Convert.ToString(view.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);};
  view.AutoResizeColumnHeadersHeight();
 }
 void MeasureGridColumns(DataGridView view){int[] widths=gridMinimums[view];for(int i=0;i<view.Columns.Count;i++){int text=TextRenderer.MeasureText(view.Columns[i].HeaderText,view.ColumnHeadersDefaultCellStyle.Font).Width;view.Columns[i].MinimumWidth=Math.Max(widths[i],text+view.ColumnHeadersDefaultCellStyle.Padding.Horizontal+16);}}
 void UpdateBatchDetails(){if(batchDetails==null||closingLayout||batchDetails.IsDisposed)return;DataGridViewRow row=batchGrid.CurrentRow;if(row==null&&batchGrid.SelectedRows.Count>0)row=batchGrid.SelectedRows[0];if(row==null){batchDetails.Text="选中一行可在这里查看和复制完整路径、输出位置与错误信息。";return;}BatchItem item=row.Tag as BatchItem;if(item!=null)batchDetails.Text="源文件："+item.Path+"\r\n输出："+item.Output+"\r\n状态："+item.State+"；尺寸："+item.Dimensions+"\r\n解码："+item.Method+"\r\n错误："+(item.Error.Length==0?"无":item.Error);}
 void RefreshAdaptiveLayout(){if(IsDisposed||Disposing||refreshingLayout||closingLayout)return;refreshingLayout=true;try{
  foreach(Label label in adaptiveLabels)FitLabel(label);
  if(batchDetails!=null)batchDetails.Height=batchDetails.Font.Height*4+18;
  foreach(Control c in Controls){Panel head=c as Panel;if(head!=null&&head.Dock==DockStyle.Top){int h=head.Padding.Vertical;foreach(Control label in head.Controls)h+=label.Height;head.Height=h;}}
  foreach(FlowLayoutPanel bar in adaptiveBars)if(!bar.IsDisposed){foreach(Control c in bar.Controls){Label l=c as Label;if(l!=null&&l.AutoSize)l.MaximumSize=new Size(Math.Max(100,bar.ClientSize.Width-bar.Padding.Horizontal-l.Margin.Horizontal),0);}bar.PerformLayout();}
  foreach(DataGridView view in new[]{grid,batchGrid,backupGrid}){MeasureGridColumns(view);view.AutoResizeColumnHeadersHeight();view.AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders);}
  foreach(KeyValuePair<TabPage,Panel> pair in layoutBodies){int fixedHeight=pair.Value.Padding.Vertical,fillHeight=200;foreach(Control c in pair.Value.Controls){if(c.Visible&&(c.Dock==DockStyle.Top||c.Dock==DockStyle.Bottom))fixedHeight+=c.Height;DataGridView view=c as DataGridView;if(view!=null)fillHeight=Math.Max(fillHeight,view.ColumnHeadersHeight+view.Font.Height*4+view.DefaultCellStyle.Padding.Vertical*4+SystemInformation.HorizontalScrollBarHeight);}int target=Math.Max(pair.Key.ClientSize.Height,fixedHeight+fillHeight);pair.Value.Height=target;pair.Key.AutoScrollMinSize=new Size(0,target);}
 }finally{refreshingLayout=false;}}
 public int LayoutAudit(string report){
  string folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"layout-evidence");Directory.CreateDirectory(folder);
  var lines=new List<string>();int failures=0;Size[] sizes={new Size(940,680),new Size(1120,790)};float[] scales={1f,1.25f,1.5f,2f};
  Font baseFont=Font;
  foreach(float scale in scales){Font=new Font(baseFont.FontFamily,10*scale);foreach(Size size in sizes){Size=size;
   foreach(TabPage page in tabs.TabPages){for(int expanded=0;expanded<(page.Text=="批量工作台"?2:1);expanded++){if(page.Text=="批量工作台")outputFolder.Parent.Visible=expanded==1;tabs.SelectedTab=page;RefreshAdaptiveLayout();Application.DoEvents();RefreshAdaptiveLayout();Application.DoEvents();
    foreach(DataGridView view in new[]{grid,batchGrid,backupGrid})if(view.Parent==layoutBodies[page]){
     foreach(DataGridViewColumn column in view.Columns){int available=column.Width-view.ColumnHeadersDefaultCellStyle.Padding.Horizontal-4;Size measured=TextRenderer.MeasureText(column.HeaderText,view.ColumnHeadersDefaultCellStyle.Font,new Size(Math.Max(10,available),Int32.MaxValue),TextFormatFlags.WordBreak);int needed=measured.Height+view.ColumnHeadersDefaultCellStyle.Padding.Vertical+2;if(view.ColumnHeadersHeight<needed){failures++;lines.Add("FAIL "+page.Text+" header "+column.HeaderText+" height="+view.ColumnHeadersHeight+" needed="+needed);}}
    }
    foreach(DataGridView view in new[]{grid,batchGrid,backupGrid})if(view.Parent==layoutBodies[page]&&view.Height<180){failures++;lines.Add("FAIL "+page.Text+" result area height="+view.Height);}
    foreach(FlowLayoutPanel bar in adaptiveBars)if(bar.Visible && bar.FindForm()==this){foreach(Control c in bar.Controls)if(c.Visible && (c.Bottom>bar.ClientSize.Height||c.Right>bar.ClientSize.Width)){failures++;lines.Add("FAIL "+page.Text+" toolbar clipped: "+c.Text+" bounds="+c.Bounds+" client="+bar.ClientSize);}}
    string suffix=scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+size.Width+"x"+size.Height+"-"+tabs.SelectedIndex+(expanded==1?"-expanded":"");
    SaveFormImage(Path.Combine(folder,suffix+".png"));
    if(layoutBodies[page].Height>page.ClientSize.Height){page.AutoScrollPosition=new Point(0,layoutBodies[page].Height);Application.DoEvents();SaveFormImage(Path.Combine(folder,suffix+"-scrolled.png"));page.AutoScrollPosition=Point.Empty;}
    lines.Add("CHECK "+page.Text+" / font "+(scale*100)+"% / "+size.Width+"x"+size.Height+" / expanded="+expanded);
   }}
  }}Font=baseFont;RefreshAdaptiveLayout();lines.Add("Total layout failures: "+failures);lines.Add("Font scaling simulation; native 125/150/200% Windows DPI sessions not independently tested.");File.WriteAllLines(report,lines.ToArray(),Encoding.UTF8);return failures==0?0:1;
 }
 void LayoutUiTests(List<string> lines){SelectPage("批量工作台");batchGrid.CurrentCell=batchRows[queue[0]].Cells[0];batchRows[queue[0]].Selected=true;UpdateBatchDetails();if(!batchDetails.Text.Contains(queue[0].Path)||!batchDetails.Text.Contains(queue[0].Output))throw new Exception("Full batch details missing");lines.Add("PASS UI full-path / output details are readable and copyable");formatSearch.Text="PSD";RefreshFormats();int count=0;foreach(DataGridViewRow row in grid.Rows)if(row.Visible)count++;if(count!=1)throw new Exception("Format filter was lost after refresh");formatSearch.Text="";lines.Add("PASS UI format filter survives refresh");RefreshAdaptiveLayout();foreach(DataGridView view in new[]{grid,batchGrid,backupGrid})if(view.ColumnHeadersHeight<view.ColumnHeadersDefaultCellStyle.Font.Height+14)throw new Exception("Header text vertical clearance insufficient");lines.Add("PASS UI header and row padding prevent vertical text clipping");if(batchSummary.Width<batchSummary.Parent.ClientSize.Width-batchSummary.Parent.Padding.Horizontal-2||batchSummary.Bottom>batchProgress.Top)throw new Exception("Status label was clipped or overlapped progress bar");lines.Add("PASS UI status label fills width and does not overlap progress");}
}
}
