﻿using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace quick_picture_viewer
{
	public partial class MainForm : Form
	{
		private string openPath;
		private int zoomFactor = 100;
		private int width = 0;
		private int height = 0;
		private Bitmap originalImage;
		private bool autoZoom = true;
		private Point panelMouseDownLocation;
		private bool fullscreen = false;
		private string currentFolder;
		private string currentFile;
		private bool alwaysOnTop = false;
		private bool imageChanged = false;
		private bool darkMode = false;
		private bool checkboardBackground = false;

		public bool printCenterImage = true;

		public MainForm(string openPath)
		{
			InitializeComponent();
			this.openPath = openPath;
		}

		private void openButton_Click_1(object sender, EventArgs e)
		{
			if (openFileDialog1.ShowDialog() == DialogResult.OK)
			{
				openFile(openFileDialog1.FileName);
			}
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			if(!string.IsNullOrEmpty(openPath))
			{
				if(File.GetAttributes(openPath).HasFlag(FileAttributes.Directory))
				{
					currentFolder = openPath;
					openFirstFileInFolder();
				}
				else
				{
					openFile(openPath);
				}
			}

			toolStrip1.Renderer = new ToolStripOverride();

			picturePanel.MouseWheel += new MouseEventHandler(picturePanel_MouseWheel);

			darkMode = ThemeManager.isDarkTheme();
			if(darkMode)
			{
				applyDarkTheme();
			}

			checkForUpdates(false);

			setAlwaysOnTop(Properties.Settings.Default.AlwaysOnTop, false);
			setCheckboardBackground(Properties.Settings.Default.CheckboardBackground, false);
		}

		public async void checkForUpdates(bool showUpToDateDialog)
		{
			try
			{
				UpdateChecker checker = new UpdateChecker("ModuleArt", "quick-picture-viewer");

				bool update = await checker.CheckUpdate();

				if (update == false)
				{
					if (showUpToDateDialog)
					{
						MessageBox.Show("Application is up to date", "Updator", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
				else
				{
					UpdateForm updateDialog = new UpdateForm(checker, "Quick Picture Viewer");

					if (alwaysOnTop)
					{
						updateDialog.TopMost = true;
					}

					var result = updateDialog.ShowDialog();
					if (result == DialogResult.Yes)
					{
						checker.DownloadAsset("QuickPictureViewer-Setup.msi");
						this.Close();
					}
					else
					{
						updateDialog.Dispose();
					}
				}
			}
			catch
			{
				if (showUpToDateDialog)
				{
					MessageBox.Show("Connection error", "Updator", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

		private void openFile(string path)
		{
			try
			{
				openImage(new Bitmap(path), Path.GetDirectoryName(path), Path.GetFileName(path));

				//RecentFilesManager.AddToRecentlyUsedDocs(path);
			}
			catch
			{
				MessageBox.Show("Unable to open this file", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void openImage(Bitmap bitmap, string directoryName, string fileName)
		{
			if (imageChanged)
			{
				var window = MessageBox.Show(
					"All unsaved data will be lost.\nAre you sure you want to open new image?",
					"Warning",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question
				);

				if (window == DialogResult.No)
				{
					return;
				}
			}

			setImageChanged(false);

			if (originalImage != null)
			{
				originalImage.Dispose();
				originalImage = null;
				pictureBox.Image.Dispose();
				pictureBox.Image = null;
			}

			originalImage = bitmap;
			pictureBox.Image = originalImage;

			width = pictureBox.Image.Size.Width;
			height = pictureBox.Image.Size.Height;
			fileLabel.Text = "File: " + fileName;

			if(directoryName == null)
			{
				currentFolder = null;
				currentFile = null;
				directoryLabel.Text = "Folder: Not exists";
				sizeLabel.Text = "Size: " + width.ToString() + " x " + height.ToString() + " px";

				nextButton.Enabled = false;
				prevButton.Enabled = false;
				deleteButton.Enabled = false;
				externalButton.Enabled = false;
				showFileButton.Enabled = false;

				dateCreatedLabel.Visible = false;
				dateModifiedLabel.Visible = false;
			}
			else
			{
				string path = Path.Combine(directoryName, fileName);

				currentFolder = directoryName;
				currentFile = fileName;
				directoryLabel.Text = "Folder: " + directoryName;
				sizeLabel.Text = "Size: " + width.ToString() + " x " + height.ToString() + " px (" + bytesToSize(path) + ")";

				nextButton.Enabled = true;
				prevButton.Enabled = true;
				deleteButton.Enabled = true;
				externalButton.Enabled = true;
				showFileButton.Enabled = true;

				dateCreatedLabel.Text = "Created: " + File.GetCreationTime(path).ToShortDateString() + " / " + File.GetCreationTime(path).ToLongTimeString();
				dateCreatedLabel.Visible = true;
				dateModifiedLabel.Text = "Modified: " + File.GetLastWriteTime(path).ToShortDateString() + " / " + File.GetLastWriteTime(path).ToLongTimeString();
				dateModifiedLabel.Visible = true;
			}

			this.Text = fileName + " - Quick Picture Viewer";

			zoomInButton.Enabled = true;
			zoomOutButton.Enabled = true;
			flipVerticalButton.Enabled = true;
			flipHorizontalButton.Enabled = true;
			rotateLeftButton.Enabled = true;
			rotateRightButton.Enabled = true;
			saveAsButton.Enabled = true;
			copyButton.Enabled = true;
			autoZoomButton.Enabled = true;
			setAsDesktopButton.Enabled = true;
			infoButton.Enabled = true;
			printButton.Enabled = true;

			zoomComboBox.Enabled = true;

			setZoomText("Auto");
		}

		private void setImageChanged(bool b)
		{
			imageChanged = b;
			hasChangesLabel.Visible = b;
		}

		private void zoomInButton_Click(object sender, EventArgs e)
		{
			zoomIn();
		}

		private void zoomOutButton_Click(object sender, EventArgs e)
		{
			zoomOut();
		}

		private void zoomIn()
		{
			setZoomText((zoomFactor + 5).ToString() + "%");
		}

		private void zoomOut()
		{
			setZoomText((zoomFactor - 5).ToString() + "%");
		}

		private void setZoomFactor(int newZoomFactor)
		{
			zoomFactor = newZoomFactor;

			zoomLabel.Text = "Zoom: " + zoomFactor.ToString() + "%";

			setAutoZoom(false);

			int newWidth = Convert.ToInt32(width * zoomFactor / 100);
			int newHeight = Convert.ToInt32(height * zoomFactor / 100);

			int newScrollH = Convert.ToInt32(picturePanel.HorizontalScroll.Value * zoomFactor / 100);
			int newScrollV = Convert.ToInt32(picturePanel.VerticalScroll.Value * zoomFactor / 100);

			Size newSize = new Size(newWidth, newHeight);

			pictureBox.Size = newSize;

			updatePictureBoxLocation();
		}
						
		private void updatePictureBoxLocation()
		{
			if (pictureBox.Width < picturePanel.Width)
			{
				pictureBox.Left = (picturePanel.Width - pictureBox.Width) / 2;
			}
			else
			{
				pictureBox.Left = 0;
			}

			if (pictureBox.Height < picturePanel.Height)
			{
				pictureBox.Top = (picturePanel.Height - pictureBox.Height) / 2;
			}
			else
			{
				pictureBox.Top = 0;
			}
		}

		private void setZoomText(string text)
		{
			zoomComboBox.Text = text;
		}

		private void setAutoZoom(bool b)
		{
			autoZoom = b;

			autoZoomButton.Checked = b;

			if (b)
			{
				pictureBox.Dock = DockStyle.Fill;

				zoomLabel.Text = "Zoom: Auto";
			}
			else
			{
				pictureBox.Dock = DockStyle.None;
			}
		}

		private void autoZoomButton_Click(object sender, EventArgs e)
		{
			if (zoomComboBox.Text == "Auto")
			{
				setZoomText("100%");
			}
			else
			{
				setZoomText("Auto");
			}
		}

		private void aboutButton_Click(object sender, EventArgs e)
		{
			AboutForm aboutBox = new AboutForm();
			aboutBox.Owner = this;
			if (alwaysOnTop)
			{
				aboutBox.TopMost = true;
			}
			aboutBox.ShowDialog();
		}

		private void setAlwaysOnTop(bool b, bool saveToDisk)
		{
			alwaysOnTop = b;
			this.TopMost = b;
			onTopButton.Checked = b;

			if(b)
			{
				setFullscreen(false);
			}

			if (saveToDisk)
			{
				Properties.Settings.Default.AlwaysOnTop = b;
				Properties.Settings.Default.Save();
			}
		}

		private void setCheckboardBackground(bool b, bool saveToDisk)
		{
			checkboardBackground = b;
			checkboardButton.Checked = b;

			if (b)
			{
				if (darkMode)
				{
					pictureBox.BackgroundImage = Properties.Resources.checkboard_dark;
				}
				else
				{
					pictureBox.BackgroundImage = Properties.Resources.checkboard_light;
				}
			}
			else
			{
				pictureBox.BackgroundImage = null;
			}

			if (saveToDisk)
			{
				Properties.Settings.Default.CheckboardBackground = b;
				Properties.Settings.Default.Save();
			}
		}

		private void flipVerticalButton_Click(object sender, EventArgs e)
		{
			originalImage.RotateFlip(RotateFlipType.RotateNoneFlipX);
			pictureBox.Image = originalImage;
			setZoomText("Auto");
			setImageChanged(true);
		}

		private void flipHorizontalButton_Click(object sender, EventArgs e)
		{
			originalImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
			pictureBox.Image = originalImage;
			setZoomText("Auto");
			setImageChanged(true);
		}

		private void rotateLeftButton_Click(object sender, EventArgs e)
		{
			originalImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
			pictureBox.Image = originalImage;
			setZoomText("Auto");
			setImageChanged(true);
		}

		private void rotateRightButton_Click(object sender, EventArgs e)
		{
			originalImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
			pictureBox.Image = originalImage;
			setZoomText("Auto");
			setImageChanged(true);
		}

		private void saveAsButton_Click(object sender, EventArgs e)
		{
			if (saveFileDialog1.ShowDialog() == DialogResult.OK)
			{
				System.IO.FileStream fs = (System.IO.FileStream) saveFileDialog1.OpenFile();
				switch (saveFileDialog1.FilterIndex)
				{
					case 1:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
						break;
					case 2:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Jpeg);
						break;
					case 3:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Gif);
						break;
					case 4:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Bmp);
						break;
					case 5:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Tiff);
						break;
					case 6:
						originalImage.Save(fs, System.Drawing.Imaging.ImageFormat.Icon);
						break;
				}
				fs.Close();
				openFile(saveFileDialog1.FileName);
			}
		}

		private void copyButton_Click(object sender, EventArgs e)
		{
			Clipboard.SetImage(originalImage);
		}

		private void pasteButton_Click(object sender, EventArgs e)
		{
			if (Clipboard.ContainsImage())
			{
				openImage(new Bitmap(Clipboard.GetImage()), null, "From clipboard");
				setImageChanged(true);
			}
			else if (Clipboard.ContainsData(DataFormats.FileDrop))
			{
				string path = ((string[]) Clipboard.GetData(DataFormats.FileDrop))[0];
				openFile(path);
			}
		}

		private void picturePanel_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && !autoZoom)
			{
				Cursor.Current = Cursors.SizeAll;

				panelMouseDownLocation = new Point(
					this.PointToClient(Cursor.Position).X + picturePanel.HorizontalScroll.Value,
					this.PointToClient(Cursor.Position).Y + picturePanel.VerticalScroll.Value
				);
			}
		}

		private void picturePanel_MouseUp(object sender, MouseEventArgs e)
		{
			Cursor.Current = Cursors.Default;
		}

		private void picturePanel_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && !autoZoom)
			{
				int newX = panelMouseDownLocation.X - this.PointToClient(Cursor.Position).X;
				int newY = panelMouseDownLocation.Y - this.PointToClient(Cursor.Position).Y;

				if (newX > picturePanel.HorizontalScroll.Minimum)
				{
					if (newX < picturePanel.HorizontalScroll.Maximum)
					{
						picturePanel.HorizontalScroll.Value = newX;
					}
					else
					{
						picturePanel.HorizontalScroll.Value = picturePanel.HorizontalScroll.Maximum;
					}
				}
				else
				{
					picturePanel.HorizontalScroll.Value = picturePanel.HorizontalScroll.Minimum;
				}

				if (newY > picturePanel.VerticalScroll.Minimum)
				{
					if (newY < picturePanel.VerticalScroll.Maximum)
					{
						picturePanel.VerticalScroll.Value = newY;
					}
					else
					{
						picturePanel.VerticalScroll.Value = picturePanel.VerticalScroll.Maximum;
					}
				}
				else
				{
					picturePanel.VerticalScroll.Value = picturePanel.VerticalScroll.Minimum;
				}
			}
		}

		private void picturePanel_MouseWheel(object sender, MouseEventArgs e)
		{
			if(Control.ModifierKeys == Keys.Control)
			{
				if (e.Delta > 0)
				{
					zoomIn();
				}
				else if(e.Delta < 0)
				{
					zoomOut();
				}
			}
		}

		private void fullscreenButton_Click(object sender, EventArgs e)
		{
			setFullscreen(!fullscreen);
		}

		private void setFullscreen(bool b)
		{
			fullscreen = b;

			this.WindowState = FormWindowState.Normal;

			toolStrip1.Visible = !b;
			statusStrip1.Visible = !b;

			if (b)
			{
				this.FormBorderStyle = FormBorderStyle.None;
				this.WindowState = FormWindowState.Maximized;

				picturePanel.Top = 0;
				picturePanel.Height = this.ClientSize.Height;
				picturePanel.BackColor = Color.Black;

				setAlwaysOnTop(false, true);
			}
			else
			{
				this.FormBorderStyle = FormBorderStyle.Sizable;

				picturePanel.Top = toolStrip1.Height;
				picturePanel.Height = this.ClientSize.Height - toolStrip1.Height - statusStrip1.Height;
				picturePanel.BackColor = Color.Transparent;
			}
		}

		private void zoomComboBox_TextChanged(object sender, EventArgs e)
		{
			try
			{
				if (zoomComboBox.Text == "Auto")
				{
					setZoomFactor(100);
					setAutoZoom(true);
				}
				else
				{
					string substr = zoomComboBox.Text.Replace("%", "");
					int zoom = int.Parse(substr);

					if (zoom < 5)
					{
						zoom = 5;
						setZoomText(zoom.ToString() + "%");
					}
					else
					{
						if (zoom > 500)
						{
							zoom = 500;
							setZoomText(zoom.ToString() + "%");
						}
						else
						{
							setZoomFactor(zoom);
						}
					}
				}				
			}
			catch
			{

			}
		}

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control)
			{
				if (e.Shift)
				{
					if (e.KeyCode == Keys.R)
					{
						rotateLeftButton.PerformClick();
					}
					else if (e.KeyCode == Keys.H)
					{
						flipVerticalButton.PerformClick();
					}
					else if (e.KeyCode == Keys.E)
					{
						showFileButton.PerformClick();
					}
					else if (e.KeyCode == Keys.C)
					{
						checkboardButton.PerformClick();
					}
				}
				else
				{
					if (e.KeyCode == Keys.O)
					{
						openButton.PerformClick();
					}
					else if (e.KeyCode == Keys.S)
					{
						saveAsButton.PerformClick();
					}
					else if (e.KeyCode == Keys.A)
					{
						autoZoomButton.PerformClick();
					}
					else if (e.KeyCode == Keys.R)
					{
						rotateRightButton.PerformClick();
					}
					else if (e.KeyCode == Keys.H)
					{
						flipHorizontalButton.PerformClick();
					}
					else if (e.KeyCode == Keys.C)
					{
						copyButton.PerformClick();
					}
					else if (e.KeyCode == Keys.V)
					{
						pasteButton.PerformClick();
					}
					else if (e.KeyCode == Keys.Oemplus)
					{
						zoomInButton.PerformClick();
					}
					else if (e.KeyCode == Keys.OemMinus)
					{
						zoomOutButton.PerformClick();
					}
					else if (e.KeyCode == Keys.T)
					{
						onTopButton.PerformClick();
					}
					else if (e.KeyCode == Keys.B)
					{
						setAsDesktopButton.PerformClick();
					}
					else if (e.KeyCode == Keys.I)
					{
						infoButton.PerformClick();
					}
					else if (e.KeyCode == Keys.E)
					{
						externalButton.PerformClick();
					}
					else if (e.KeyCode == Keys.P)
					{
						printButton.PerformClick();
					}
					//else if (e.KeyCode == Keys.N)
					//{
					//	Process.Start("quick-picture-viewer.exe");
					//}
				}
			}
			else if (e.Alt)
			{
				if (e.KeyCode == Keys.Enter)
				{
					fullscreenButton.PerformClick();
				}
			}
			else
			{
				if (e.KeyCode == Keys.F || e.KeyCode == Keys.F11)
				{
					fullscreenButton.PerformClick();
				}
				else if (e.KeyCode == Keys.F12)
				{
					screenshotButton.PerformClick();
				}
				else if (e.KeyCode == Keys.Delete)
				{
					deleteButton.PerformClick();
				}
				else if (e.KeyCode == Keys.F1)
				{
					aboutButton.PerformClick();
				}
				else if (e.KeyCode == Keys.Left)
				{
					prevButton.PerformClick();
				}
				else if (e.KeyCode == Keys.Right)
				{
					nextButton.PerformClick();
				}
				else if (e.KeyCode == Keys.Escape)
				{
					setFullscreen(false);
				}
				else if (e.KeyCode == Keys.Down)
				{
					zoomOut();
				}
				else if (e.KeyCode == Keys.Up)
				{
					zoomIn();
				}
			}
		}

		private void MainForm_DragDrop(object sender, DragEventArgs e)
		{
			Bitmap bitmap = e.Data.GetData(DataFormats.Bitmap) as Bitmap;
			string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];

			if (bitmap != null)
			{
				openImage(bitmap, null, "Dragged image");
				setImageChanged(true);
			}
			else if (files.Length > 0)
			{
				string path = files[0];
				openFile(path);
			}
		}

		private void MainForm_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap))
			{
				e.Effect = DragDropEffects.All;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}

		private void onTopButton_Click(object sender, EventArgs e)
		{
			setAlwaysOnTop(!alwaysOnTop, true);
		}

		private void screenshotButton_Click(object sender, EventArgs e)
		{
			this.Hide();

			System.Threading.Thread.Sleep(100);

			Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
			using (Graphics g = Graphics.FromImage(bmp))
			{
				g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);

				openImage(bmp, null, "Captured screen");
				setImageChanged(true);
			}

			this.Show();
		}

		private int nextFile()
		{
			string[] filePaths = getCurrentFiles();

			int currentIndex = 0;
			for (int i = 0; i < filePaths.Length; i++)
			{
				if (filePaths[i] == Path.Combine(currentFolder, currentFile))
				{
					currentIndex = i;
					break;
				}
			}

			if (currentIndex == filePaths.Length - 1)
			{
				openFile(filePaths[0]);
			}
			else
			{
				openFile(filePaths[currentIndex + 1]);
			}

			return filePaths.Length;
		}

		private void nextButton_Click(object sender, EventArgs e)
		{
			nextFile();
		}

		private void prevButton_Click(object sender, EventArgs e)
		{
			string[] filePaths = getCurrentFiles();

			int currentIndex = 0;
			for (int i = 0; i < filePaths.Length; i++)
			{
				if (filePaths[i] == Path.Combine(currentFolder, currentFile))
				{
					currentIndex = i;
					break;
				}
			}

			if (currentIndex == 0)
			{
				openFile(filePaths[filePaths.Length - 1]);
			}
			else
			{
				openFile(filePaths[currentIndex - 1]);
			}
		}

		private string[] getCurrentFiles()
		{
			string[] exts = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".exif", ".gif", ".bmp", ".dib", ".rle" };
			List<string> arlist = new List<string>();

			string[] allFiles = Directory.GetFiles(currentFolder);
			for(int i = 0; i < allFiles.Length; i++)
			{
				string ext = Path.GetExtension(allFiles[i]);
				if (exts.Contains(ext))
				{
					arlist.Add(allFiles[i]);
				}
			}

			return arlist.ToArray();
		}

		private void openFirstFileInFolder()
		{
			string[] filePaths = getCurrentFiles();

			if(filePaths.Length > 0)
			{
				openFile(filePaths[0]);
			}
			else
			{
				MessageBox.Show("Directory is empty", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void setAsDesktopButton_Click(object sender, EventArgs e)
		{
			WallpaperForm wallpaperForm = new WallpaperForm(originalImage);
			if (alwaysOnTop)
			{
				wallpaperForm.TopMost = true;
			}
			wallpaperForm.ShowDialog();
		}

		private void deleteButton_Click(object sender, EventArgs e)
		{
			DialogResult d = MessageBox.Show(
				"Are you sure you want to move this file to the Recyble Bin?",
				"Delete file",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question
			);

			if (d == DialogResult.Yes)
			{
				string path = Path.Combine(currentFolder, currentFile);
				if (FileSystem.FileExists(path))
				{
					originalImage.Dispose();
					originalImage = null;
					pictureBox.Image.Dispose();
					pictureBox.Image = null;

					if (nextFile() <= 1)
					{
						closeFile();
					}
					FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
				}
				else
				{
					MessageBox.Show("File not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void closeFile()
		{
			originalImage.Dispose();
			originalImage = null;
			pictureBox.Image.Dispose();
			pictureBox.Image = null;

			saveAsButton.Enabled = false;
			deleteButton.Enabled = false;
			prevButton.Enabled = false;
			nextButton.Enabled = false;
			autoZoomButton.Enabled = false;
			zoomInButton.Enabled = false;
			zoomOutButton.Enabled = false;
			rotateLeftButton.Enabled = false;
			rotateRightButton.Enabled = false;
			flipHorizontalButton.Enabled = false;
			flipVerticalButton.Enabled = false;
			copyButton.Enabled = false;
			setAsDesktopButton.Enabled = false;
			infoButton.Enabled = false;
			externalButton.Enabled = false;
			printButton.Enabled = false;
			showFileButton.Enabled = false;

			zoomComboBox.Enabled = false;

			directoryLabel.Text = "Folder: Empty";
			fileLabel.Text = "File: Empty";
			sizeLabel.Text = "Size: 0 x 0 px";
			dateCreatedLabel.Visible = false;
			dateModifiedLabel.Visible = false;

			setZoomText("Auto");
		}

		private string bytesToSize(string path)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = new FileInfo(path).Length;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len = len / 1024;
			}

			return String.Format("{0:0.##} {1}", len, sizes[order]);
		}

		private void infoButton_Click(object sender, EventArgs e)
		{
			InfoForm infoForm = new InfoForm(originalImage, currentFolder, currentFile);
			if (alwaysOnTop)
			{
				infoForm.TopMost = true;
			}
			infoForm.ShowDialog();
		}

		private void picturePanel_DoubleClick(object sender, EventArgs e)
		{
			fullscreenButton.PerformClick();
		}

		private void showOpenWithDialog(string path)
		{
			var args = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
			args += ",OpenAs_RunDLL " + path;
			Process.Start("rundll32.exe", args);
		}

		private void externalButton_Click(object sender, EventArgs e)
		{
			showOpenWithDialog(Path.Combine(currentFolder, currentFile));
		}

		private void applyDarkTheme()
		{
			this.ForeColor = Color.White;
			this.BackColor = ThemeManager.BackColorDark;
			toolStrip1.BackColor = ThemeManager.MainColorDark;
			statusStrip1.BackColor = ThemeManager.SecondColorDark;

			openButton.Image = Properties.Resources.white_open;
			saveAsButton.Image = Properties.Resources.white_saveas;
			printButton.Image = Properties.Resources.white_print;
			deleteButton.Image = Properties.Resources.white_trash;
			externalButton.Image = Properties.Resources.white_popup;

			prevButton.Image = Properties.Resources.white_prev;
			showFileButton.Image = Properties.Resources.white_picfolder;
			nextButton.Image = Properties.Resources.white_next;

			autoZoomButton.Image = Properties.Resources.white_autozoom;
			zoomInButton.Image = Properties.Resources.white_zoomin;
			zoomOutButton.Image = Properties.Resources.white_zoomout;

			rotateLeftButton.Image = Properties.Resources.white_rotatel;
			rotateRightButton.Image = Properties.Resources.white_rotater;
			flipHorizontalButton.Image = Properties.Resources.white_fliph;
			flipVerticalButton.Image = Properties.Resources.white_flipv;

			screenshotButton.Image = Properties.Resources.white_screenshot;
			infoButton.Image = Properties.Resources.white_info;
			copyButton.Image = Properties.Resources.white_copy;
			pasteButton.Image = Properties.Resources.white_paste;

			checkboardButton.Image = Properties.Resources.white_grid;
			fullscreenButton.Image = Properties.Resources.white_fullscreen;
			onTopButton.Image = Properties.Resources.white_ontop;

			setAsDesktopButton.Image = Properties.Resources.white_desktop;

			aboutButton.Image = Properties.Resources.white_about;

			directoryLabel.Image = Properties.Resources.white_picfolder;
			fileLabel.Image = Properties.Resources.white_imgfile;
			sizeLabel.Image = Properties.Resources.white_size;
			zoomLabel.Image = Properties.Resources.white_zoom;
			dateCreatedLabel.Image = Properties.Resources.white_clock;
			dateModifiedLabel.Image = Properties.Resources.white_history;
			hasChangesLabel.Image = Properties.Resources.white_erase;
		}

		private void MainForm_SizeChanged(object sender, EventArgs e)
		{
			if(!autoZoom)
			{
				updatePictureBoxLocation();
			}
		}

		private void printButton_Click(object sender, EventArgs e)
		{
			PrintForm pf = new PrintForm(printDocument1);
			pf.Owner = this;
			if (alwaysOnTop)
			{
				pf.TopMost = true;
			}
			if (pf.ShowDialog() == DialogResult.OK)
			{
				if (printDialog1.ShowDialog() == DialogResult.OK)
				{
					printDocument1.Print();
				}
			}
		}

		private void printDocument1_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
		{
			RectangleF marginBounds = e.MarginBounds;
			RectangleF printableArea = e.PageSettings.PrintableArea;

			int availableWidth = (int)Math.Floor(printDocument1.OriginAtMargins
				? marginBounds.Width
				: (e.PageSettings.Landscape
					? printableArea.Height
					: printableArea.Width));

			int availableHeight = (int)Math.Floor(printDocument1.OriginAtMargins
				? marginBounds.Height
				: (e.PageSettings.Landscape
					? printableArea.Width
					: printableArea.Height));

			if (availableWidth > originalImage.Width && availableHeight > originalImage.Height)
			{
				int x = 0;
				int y = 0;
				if (printCenterImage)
				{
					x = (availableWidth - originalImage.Width) / 2;
					y = (availableHeight - originalImage.Height) / 2;
				}
				e.Graphics.DrawImage(originalImage, x, y, originalImage.Width, originalImage.Height);
			} 
			else
			{
				double scaleW = availableWidth / (double) originalImage.Width;
				double scaleH = availableHeight / (double) originalImage.Height;

				if (scaleW < scaleH)
				{
					int x = 0;
					int y = 0;
					if (printCenterImage)
					{
						x = (availableWidth - Convert.ToInt32(originalImage.Width * scaleW)) / 2;
						y = (availableHeight - Convert.ToInt32(originalImage.Height * scaleW)) / 2;
					}
					e.Graphics.DrawImage(originalImage, x, y, availableWidth, Convert.ToInt32(originalImage.Height * scaleW));
				}
				else
				{
					int x = 0;
					int y = 0;
					if (printCenterImage)
					{
						x = (availableWidth - Convert.ToInt32(originalImage.Width * scaleH)) / 2;
						y = (availableHeight - Convert.ToInt32(originalImage.Height * scaleH)) / 2;
					}
					e.Graphics.DrawImage(originalImage, x, y, Convert.ToInt32(originalImage.Width * scaleH), availableHeight);
				}
			}
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if(imageChanged)
			{
				var window = MessageBox.Show(
					"All unsaved data will be lost.\nAre you sure you want to close the application?",
					"Warning",
					MessageBoxButtons.YesNo, 
					MessageBoxIcon.Question
				);

				e.Cancel = (window == DialogResult.No);
			}
		}

		private void showFileButton_Click(object sender, EventArgs e)
		{
			string argument = "/select, \"" + Path.Combine(currentFolder, currentFile) + "\"";
			Process.Start("explorer.exe", argument);
		}

		private void checkboardButton_Click(object sender, EventArgs e)
		{
			setCheckboardBackground(!checkboardBackground, true);
		}
	}
}
