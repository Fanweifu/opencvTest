﻿using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Emgu.CV.CvEnum;
using Accord.MachineLearning.VectorMachines;

namespace ShowOpenCVResult.Windows
{
    public partial class FindContours : MoveBlock
    {
        int selectIndex = -1;
        VectorOfVectorOfPoint cons = new VectorOfVectorOfPoint();
        VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();
        List<int> dels = new List<int>();
        VectorOfPoint m_modeLine ;
        Image<Bgr, byte> m_filesrc;
        Image<Gray, byte> m_filegray;
        Image<Gray, byte> m_modeFile;
        Mat layerstrcut = null;
        private List<double> m_i1 = new List<double>();
        private List<double> m_i2 = new List<double>();
        private List<double> m_i3 = new List<double>();
        MulticlassSupportVectorMachine m_svm = null;

        public FindContours()
        {
            InitializeComponent();
        }

        private void imageIOControl1_DoImgChange(object sender, EventArgs e)
        {
            if (m_filesrc == null) return;

            Image<Gray, byte> imgcan = m_filegray.Clone();
            Image<Bgr, byte> imgback = m_filesrc.Clone();
            CvInvoke.Threshold(imgcan, imgcan, 130, 255, ThresholdType.Binary);
            cons = GetTreeData(imgcan,out layerstrcut);
            myTrackBarEpsilon_ValueChanged(null,null);
            doSelect();
            imgcan.Dispose();

            CvInvoke.DrawContours(imgback, cons, -1, new MCvScalar(0, 0, 255), 2, LineType.FourConnected);

            if (imageIOControl1.InImage != null)
            {
                imageIOControl1.InImage.Dispose();
            }

            imageIOControl1.InImage = imgback;
            
            myTrackBar6.Enabled = false;
        }

        /// <summary>
        /// 获得轮廓
        /// </summary>
        /// <param name="image">灰度图</param>
        /// <param name="b">表达树状数据的数组</param>
        /// <returns>VectorOfVectorOfPoint类型的轮廓数据</returns>
        
        VectorOfVectorOfPoint GetTreeData(Image<Gray, byte> image, out Mat hirerarchy)
        {
            hirerarchy = new Mat();
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(image, contours, hirerarchy, RetrType.Tree, ChainApproxMethod.ChainApproxSimple);
            return contours;
        }

        void selectCons(VectorOfVectorOfPoint vvp, Mat hirerarchy ,int minarea ,int maxarea,int minlength,int maxlength,double minarearate,double maxAreaToLength,ref int[] array) {
            if (vvp == null || vvp.Size == 0) return;
            if (hirerarchy == null || hirerarchy.IsEmpty) return;
            int [] resultarray  = new int[hirerarchy.Cols * 4];
            hirerarchy.CopyTo(resultarray);
            dels.Clear();

            for (int i = 0; i < vvp.Size; i++)
            {
                double area = CvInvoke.ContourArea(vvp[i]);
                double length = CvInvoke.ArcLength(vvp[i], true);
                var rect = CvInvoke.MinAreaRect(vvp[i]);
                double rate = area / (rect.Size.Width * rect.Size.Height);
                bool isneed = area >= minarea && area <= maxarea && length >= minlength && length <= maxlength && rate >= minarearate && area / length < maxAreaToLength;

                if (!isneed)
                {
                    dels.Add(i);
                }
            }

            int bcnt = resultarray.Count();
            for (int i = 0; i < bcnt; i++)
            {
                if (dels.Contains(i / 4))
                {
                    resultarray[i] = -2;
                }
                if (dels.Contains(resultarray[i]))
                {
                    resultarray[i] = -1;
                }
            }
            array = resultarray;
        }

        void doSelect() {
            if (cons == null || cons.Size == 0) return;
            int[] layerresult = null;
            selectCons(cons, layerstrcut, minAreabar.Value, maxAreabar.Value, minLengthBar.Value, maxLengthbar.Value, (double)minAreaRatebar.Value/100,maxAreaToLenght.Value,ref layerresult);
            editNode(layerresult, treeView1);

        }

        private void myTrackBarEpsilon_ValueChanged(object sender, EventArgs e)
        {
            if (m_filesrc == null || cons == null || cons.Size == 0) return;

            vvp = new VectorOfVectorOfPoint();
            for (int i = 0; i < cons.Size; i++)
            {
                if (cons[i].Size > 0)
                {
                    VectorOfPoint vp = new VectorOfPoint();
                    CvInvoke.ApproxPolyDP(cons[i], vp, (double)epsilonbar.Value / 100, true);
                    vvp.Push(vp);
                }
            }
            var imgback = m_filesrc.Clone();

            CvInvoke.DrawContours(imgback, vvp, -1, new MCvScalar(0, 0, 255), 2, LineType.FourConnected);

            if (imageIOControl1.InImage != null)
            {
                imageIOControl1.InImage.Dispose();
            }

            imageIOControl1.InImage = imgback;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string path = OpencvForm.SelectImg();
            if (path == null) return;
            m_filesrc = new Image<Bgr, byte>(path);
            m_filegray = m_filesrc.Convert<Gray, byte>();
            imageIOControl1.DoChange();
        }

        /// <summary>
        /// 将数组以树状显示在treeView1中
        /// </summary>
        /// <param name="a">数组</param>
        /// <param name="treeView1">控件</param>
        void editNode(int[] a, TreeView treeView1)
        {
            treeView1.Nodes.Clear();
            int cnt = a.Count() / 4;
            TreeNode[] s = new TreeNode[cnt];
            for (int i = 0; i <= cnt - 1; i++) {
                s[i] = new TreeNode(i.ToString());
            }
            for (int i = 0; i < cnt; i++) {
                if (a[i * 4 + 3] == -1){
                        treeView1.Nodes.Add(s[i]);
                    
                }
                    
                else {
                    if (a[i * 4 + 3]>=0&&!s[a[i * 4 + 3]].Nodes.Contains(s[i]) && s[i].Parent==null)
                        s[a[i * 4 + 3]].Nodes.Add(s[i]);
                }
            }

            for (int i = 0; i < cnt; i++)
            {
                if (a[i * 4 + 2] < 0) { 
                    
                }

                else
                {
                    if (!s[i].Nodes.Contains(s[a[i * 4 + 2]]) && s[a[i * 4 + 2]].Parent==null)
                        s[i].Nodes.Add(s[a[i * 4 + 2]]);
                }
            }

            for (int i = 0; i < cnt; i++)
            {
                if (a[i * 4] < 0)
                {

                }

                else
                {
                    if (s[i].Parent != null) {
                        s[a[i * 4]].Remove();
                        int index = s[i].Index;
                        s[i].Parent.Nodes.Insert(index + 1, s[a[i * 4]]);
                        
                    }
                }
            }
            for (int i = 0; i < cnt; i++)
            {
                if (a[i * 4+1] < 0 )
                {

                }

                else
                {
                    if (s[i].Parent != null)
                    {
                        s[a[i * 4 + 1]].Remove();
                        int index = s[i].Index;
                        s[i].Parent.Nodes.Insert(index, s[a[i * 4+1]]);

                    }
                }
            }

        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            int index = 0;
            if (!int.TryParse(e.Node.Text, out index)) return;
            
            var img = m_filesrc.Clone();
            CvInvoke.DrawContours(img,vvp , index, new MCvScalar(0, 255, 0), 2);

            if (tsbtnRotateRect.Checked)
            {
                var rr = CvInvoke.MinAreaRect(cons[index]);
                OpencvMath.DrawRotatedRect(rr, img);
                tsslblRectAngle.Text = rr.Angle.ToString("0.00");
                tsslblRectSize.Text = rr.Size.ToString();
            }

            if (tsbtnWeightCentre.Checked)
            {
                var moment = CvInvoke.Moments(cons[index]);
                Point p = new Point((int)(moment.M10 / moment.M00), (int)(moment.M01 / moment.M00));
                CvInvoke.Circle(img, p, 1, new MCvScalar(255, 0, 0));
            }

            if (tsbtnFitLine.Checked)
            {
                PointF dir = new PointF(), point = new PointF();
                Point[] pt = cons[index].ToArray();
                int cnt = pt.Count();
                PointF[] ptf = new PointF[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    ptf[i] = new PointF(pt[i].X, pt[i].Y);
                }
                CvInvoke.FitLine(ptf, out dir, out point, DistType.C,0, 0.01,0.01);
                CvInvoke.Line(img, new Point((int)point.X, (int)point.Y), new Point((int)(point.X + dir.X), (int)(point.Y + dir.Y)),new MCvScalar(0,0,255));
            }
            

            selectIndex = index;
            Calfam(cons[index]);

            

            imageIOControl1.InImage = img;

            //if (tsbtnLookWhenSelect.Checked)
            //{
            //    Mat a = OpencvMath.GetSquareExampleImg(cons[index], m_filesrc.Size);

            //    new Thread(() =>
            //    {
            //        ImageViewer.Show(a);
            //    }
            //    ).Start();
            //}


            
        }

        private void Calfam(VectorOfPoint vp) {
            if (m_modeLine != null && m_modeLine.Size > 0)
            {
                double val = CvInvoke.MatchShapes(vp, m_modeLine, (ContoursMatchType)comboBox1.SelectedItem) ;
                tslblFm.Text = val.ToString("0.000");
   
            }
        
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            string path = OpencvForm.SelectImg();
            if (path == null) return;
            m_modeFile = extensionImg(new Image<Gray, byte>(path));
            VectorOfPoint vp = getModelLine(m_modeFile);
            if (vp == null) return;
            m_modeLine = new VectorOfPoint();
            CvInvoke.ApproxPolyDP(vp, m_modeLine, (double)epsilonbar.Value / 100, true);
            tslblModeFilePath.Text = path;
            Image<Bgr, byte> draw = m_modeFile.Clone().Convert<Bgr, byte>();
            draw.DrawPolyline(m_modeLine.ToArray(), true, new Bgr(0, 0, 255), 3);
            imageIOControl1.OutImage = draw;
            groupBox1.Enabled = true;
        }

        VectorOfPoint getModelLine(Image<Gray,byte> input, int minpts =10,int maxpts = 3000) {
            Image<Gray, byte> bin = input.ThresholdBinary(new Gray(100), new Gray(255));
            VectorOfVectorOfPoint lines = new VectorOfVectorOfPoint();

            CvInvoke.FindContours(bin, lines, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            for (int i = 0; i < lines.Size; i++) {
                int cnt = lines[i].Size;
                if (cnt > minpts && cnt < maxpts) {
                    return lines[i];
                }
            }
            return null;
        }

        Image<Gray, byte> extensionImg( Image<Gray, byte> inputImg,int exsize = 10) {
            Image<Gray, byte> eximg = new Image<Gray, byte>(inputImg.Width + exsize * 2, inputImg.Height + exsize * 2);
            for (int i = 0; i < inputImg.Height; i++)
                for (int j = 0; j < inputImg.Width; j++)
                {
                    eximg[i + exsize, j + exsize] = inputImg[i, j];
                }
            return eximg;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(cons==null||cons.Size==0) return;
            if(m_modeLine==null||m_modeLine.Size ==0) return;

            switch ((ContoursMatchType)comboBox1.SelectedItem) {

                case ContoursMatchType.I1:
                    m_i1.Clear();
                    for (int i = 0; i < cons.Size; i++) {
                        m_i1.Add(CvInvoke.MatchShapes(cons[i], m_modeLine, ContoursMatchType.I1));
                    }
                    myTrackBar6.Maximum = (int)(m_i1.Max() * 100);
                    break;
                case ContoursMatchType.I2:
                      m_i2.Clear();
                    for (int i = 0; i < cons.Size; i++) {
                        m_i2.Add(CvInvoke.MatchShapes(cons[i], m_modeLine, ContoursMatchType.I2));
                    }
                    myTrackBar6.Maximum = (int)(m_i2.Max() * 100);
                    break;
                case ContoursMatchType.I3:
                    m_i3.Clear();
                    for (int i = 0; i < cons.Size; i++)
                    {
                        m_i3.Add(CvInvoke.MatchShapes(cons[i], m_modeLine, ContoursMatchType.I3));
                    }
                    myTrackBar6.Maximum = (int)(m_i3.Max() * 100);
                    break;
            }
            myTrackBar6.Enabled = true;
        }

        private void myTrackBar6_ValueChanged(object sender, EventArgs e)
        {
            Image<Bgr, byte> selectimg = m_filesrc.Clone();
            switch((ContoursMatchType)comboBox1.SelectedItem){
                case ContoursMatchType.I1:
                    for (int i = 0; i < cons.Size; i++) {
                        if (m_i1[i] < (double)myTrackBar6.Value / 100) {
                            selectimg.DrawPolyline(cons[i].ToArray(), true, new Bgr(0, 255, 255), 2);
                        }
                    }
                break;
                case ContoursMatchType.I2:
                for (int i = 0; i < cons.Size; i++)
                {
                    if (m_i2[i] < (double)myTrackBar6.Value / 100)
                    {
                        selectimg.DrawPolyline(cons[i].ToArray(), true, new Bgr(0, 255, 255), 2);
                    }
                }
                break;
                case ContoursMatchType.I3:
                for (int i = 0; i < cons.Size; i++)
                {
                    if (m_i3[i] < (double)myTrackBar6.Value / 100)
                    {
                        selectimg.DrawPolyline(cons[i].ToArray(), true, new Bgr(0, 255, 255), 2);
                    }
                }
                break;

            
            }
            
            imageIOControl1.InImage = selectimg;
        }

        private void setRange(bool resetvalue = false)
        {
            if (m_filesrc == null) return;

            minAreabar.Maximum = m_filesrc.Width * m_filesrc.Height / 10;        
            maxAreabar.Maximum = m_filesrc.Width * m_filesrc.Height;
            minLengthBar.Maximum = 100;
            maxLengthbar.Maximum = (m_filesrc.Width + m_filesrc.Height) * 2;
            minAreaRatebar.Maximum = 100;

            if (resetvalue)
            {
                minAreabar.Value = 0;
                maxAreabar.Value = m_filesrc.Width * m_filesrc.Height;
                minLengthBar.Value = 0;
                maxLengthbar.Value = (m_filesrc.Width + m_filesrc.Height) * 2;
                minAreaRatebar.Value = 0;

            }

        }

        private void imageIOControl1_AfterImgLoaded(object sender, EventArgs e)
        {
            m_filesrc = (imageIOControl1.InImage as Image<Bgr, byte>).Clone();
            m_filegray = m_filesrc.Convert<Gray, byte>();
            setRange();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            if (cons != null && selectIndex < cons.Size)
            {
                Mat result = OpencvMath.GetSquareExampleImg(vvp[selectIndex], RoadTransform.ExampleSize);
                imageIOControl1.OutImage = result;
            }
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            if (cons == null) return;
            long time = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Mat result = TestClass.JugdeTest(cons, m_filesrc.Mat, ref time);
            sw.Stop();
            MessageBox.Show(string.Format("耗时{0}毫秒", sw.ElapsedMilliseconds));

            
            imageIOControl1.InImage = result;
        }

        private void toolStripButton2_Click_1(object sender, EventArgs e)
        {
            if (m_filesrc == null) return;
            Image<Gray, byte> gray = m_filesrc.Convert<Gray, byte>();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            VectorOfVectorOfPoint vvp = OpencvMath.WalkRoadImg(gray.Mat);
            for (int i = 0; i < vvp.Size; i++)
            {
                Point[] pts = vvp[i].ToArray();
                int cnt = pts.Count();
                OpencvMath.DrawRotatedRect(CvInvoke.MinAreaRect(vvp[i]), gray);
                for (int j = 0; j < cnt; j++)
                {
                    CvInvoke.Circle(gray, pts[j], 3, new MCvScalar(200), 3);
                }

            }
        
            sw.Stop();
            MessageBox.Show(string.Format("耗时:{0}", sw.ElapsedMilliseconds));
            imageIOControl1.OutImage = gray;
        }

        private void FindContours_Load(object sender, EventArgs e)
        {
            comboBox1.DataSource = Enum.GetValues(typeof(ContoursMatchType));
            var config = ShowOpenCVResult.Properties.Settings.Default;

            maxAreabar.Value = (int)config.MaxArea;
            minAreabar.Value = (int)config.MinArea;
            maxAreaToLenght.Value = (int)(config.MaxAreaToLength);
            minAreaRatebar.Value = (int)(config.MinRateToRect*100);
            maxLengthbar.Value = (int)config.MaxLength;
            minLengthBar.Value = (int)config.MinLength;
            epsilonbar.Value = (int)(config.Epsilon*100);

        }

        private void myTrackBar5_ValueChanged(object sender, EventArgs e)
        {
            if (cons == null || m_filesrc==null) return;
            var img = m_filesrc.Clone();

            doSelect();
            for (int i = 0; i < cons.Size; i++)
            {
                if (dels != null && dels.Contains(i)) continue;
                CvInvoke.DrawContours(img, vvp , i, new MCvScalar(0, 0, 255), 2);
       
            }
            if (imageIOControl1.InImage!=null)
            {
                imageIOControl1.InImage.Dispose();   
            }
            imageIOControl1.InImage = img;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var config = ShowOpenCVResult.Properties.Settings.Default;
            config.MaxArea = maxAreabar.Value;
            config.MinArea = minAreabar.Value;
            config.MaxAreaToLength = maxAreaToLenght.Value;
            config.MinRateToRect = (double)minAreaRatebar.Value / 100;
            config.MaxLength = maxLengthbar.Value;
            config.MinLength = minLengthBar.Value;
            config.Epsilon = (double)epsilonbar.Value / 100;
            config.Save();
        }

        private void toolStripButton3_Click_1(object sender, EventArgs e)
        {
            m_svm = MulticlassSupportVectorMachine.Load("svm.dat");
            //using (OpenFileDialog of = new OpenFileDialog())
            //{
            //    of.Filter = "DAT|*.dat";
            //    if (of.ShowDialog() != DialogResult.OK) return;
  
            //}

        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            Mat imgback = new Mat(m_filegray.Size, DepthType.Cv8U, 3);
            if (m_filegray == null || m_svm == null) return;
            for (int i = 0; i < vvp.Size; i++)
            {
                if (dels.Contains(i)) continue;

                Mat result = OpencvMath.GetSquareExampleImg(vvp[i], RoadTransform.ExampleSize);
                double[] array = OpencvMath.extract(result);
                int lebel = m_svm.Compute(array, MulticlassComputeMethod.Elimination);
                CvInvoke.DrawContours(imgback, vvp, i,OpencvMath.getcolor(lebel), -1);
                
            }
            imageIOControl1.OutImage = imgback;
            
        }

       
    }
}
