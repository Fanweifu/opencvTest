﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Emgu;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;
using ShowOpenCVResult.Properties;
using Accord.MachineLearning.VectorMachines;

namespace ShowOpenCVResult
{
    public enum RoadObjectType
    {
        FullLine,//实线
        PartOfDottedLine,//虚线
        SignInLoad,//路面标志
        Unkown,// 未知
    }

    public static class OpencvMath
    {
        static Mat m_strcut = null;

        public static Mat Struct
        {
            get
            {
                if (m_strcut == null) { m_strcut = CvInvoke.GetStructuringElement(ElementShape.Cross, new Size(3, 3), new Point(-1, -1)); }
                return m_strcut;
            }
        }


        #region Contours


        public static Mat GetDefaultRoi(Mat img)
        {
            return new Mat(img, Properties.Settings.Default.DetectArea);
        }
        static public void DrawRotatedRect(RotatedRect rr, IInputOutputArray backimg, int kickness = 2)
        {
            PointF[] pts = rr.GetVertices();
            Point[] intpts = new Point[4];
            for (int i = 0; i < 4; i++)
            {
                intpts[i] = new Point((int)pts[i].X, (int)pts[i].Y);
            }
            for (int i = 0; i < 4; i++)
            {
                CvInvoke.Line(backimg, intpts[i % 4], intpts[(i + 1) % 4], new MCvScalar(255, 0, 0), 2);
            }
        }
        static public void DrawRotatedRectInGray(RotatedRect rr, IInputOutputArray backimg, int kickness = 2)
        {
            PointF[] pts = rr.GetVertices();
            Point[] intpts = new Point[4];
            for (int i = 0; i < 4; i++)
            {
                intpts[i] = new Point((int)pts[i].X, (int)pts[i].Y);
            }
            for (int i = 0; i < 4; i++)
            {
                CvInvoke.Line(backimg, intpts[i % 4], intpts[(i + 1) % 4], new MCvScalar(127), 2);
            }
        }
        static public Mat GetSquareExampleImg(VectorOfPoint vp, Size size, bool needthreshold = false)
        {
            var rightvp = AngleAdjustVp(vp);
            Rectangle vpr = CvInvoke.BoundingRectangle(rightvp);
            Point[] pts = rightvp.ToArray();
            for (int i = 0; i < rightvp.Size; i++)
            {
                pts[i].X -= vpr.X;
                pts[i].Y -= vpr.Y;
            }
            VectorOfPoint adjust = new VectorOfPoint(pts);
            Mat gray = new Mat(vpr.Size, DepthType.Cv8U, 1);
            gray.SetTo(default(MCvScalar));
            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();
            vvp.Push(adjust);
            CvInvoke.DrawContours(gray, vvp, 0, new MCvScalar(255), -1, LineType.AntiAlias);
            int w = gray.Width, h = gray.Height;
            Mat r;
            if (w >= h)
            {
                r = new Image<Gray, byte>(w, w).Mat;
                Mat roi = new Mat(r, new Rectangle(0, (w - h) / 2, w, h));
                gray.CopyTo(roi, null);

            }
            else
            {
                r = new Image<Gray, byte>(h, h).Mat;
                Mat roi = new Mat(r, new Rectangle((h - w) / 2, 0, w, h));
                gray.CopyTo(roi, null);

            }
            vvp.Dispose();
            Mat result = r.Clone();
            if (result.Size != size)
                CvInvoke.Resize(result, result, size, 0, 0, Inter.Nearest);
            if (needthreshold)
            {
                CvInvoke.Threshold(result, result, 127, 255, ThresholdType.Binary);
            }

            return result;
        }
        static public VectorOfPoint AngleAdjustVp(VectorOfPoint vp)
        {
            RotatedRect rr = CvInvoke.MinAreaRect(vp);
            double angle = rr.Size.Width > rr.Size.Height ? 90.0 + rr.Angle : rr.Angle;
            PointF center = rr.Center;
            Mat rote = new Mat();
            CvInvoke.GetRotationMatrix2D(center, angle, 1, rote);
            PointF[] pfs = Array.ConvertAll<Point, PointF>(vp.ToArray(), (Point x) => { return new PointF(x.X, x.Y); });
            VectorOfPointF dst = new VectorOfPointF();
            CvInvoke.Transform(new VectorOfPointF(pfs), dst, rote);
            Point[] resultpts = Array.ConvertAll<PointF, Point>(dst.ToArray(), Point.Round);
            return new VectorOfPoint(resultpts);
        }
        //static public Bitmap GetContours(Image<Gray, byte> wrapimg, TreeNodeCollection root, ref Point[][] ptss, ref int[] del, bool needSelect = false)
        //{
        //    //if (wrapimg == null) throw new ArgumentException("Img is Empty");
        //    //long time = 0;
        //    //LineSegment2D[] lns = null;
        //    //Mat result = SpeedProcessNoWarp(wrapimg, out time, out lns);
        //    //CvInvoke.Threshold(result, result, 130, 255, ThresholdType.Binary);
        //    //int[] layer = null;
        //    //var vvp = getCons(result, ref layer, ref del, needSelect);
        //    //editNode(layer, root);
        //    //ptss = vvp.ToArrayOfArray();
        //    //var grayimg = result.ToImage<Gray, byte>();
        //    //var map = grayimg.ToBitmap();
        //    //result.Dispose();
        //    //grayimg.Dispose();
        //    //return map;
        //}
        //static public Bitmap GetContours(string path, TreeNodeCollection root, ref Point[][] pts, ref int[] del)
        //{
        //    return GetContours(new Image<Bgr, byte>(path), root, ref pts, ref del);
        //}
        static public VectorOfVectorOfPoint FindMaxAreaIndexCon(Mat img, out int index)
        {
            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(img, vvp, null, RetrType.List, ChainApproxMethod.ChainApproxNone);
            double max = 0;
            int maxindex = 0;
            for (int i = 0; i < vvp.Size; i++)
            {
                double area = CvInvoke.ContourArea(vvp[i]);
                if (area > max)
                {
                    max = area;
                    maxindex = i;
                }
            }
            index = maxindex;
            return vvp;

        }
        static VectorOfVectorOfPoint getCons(Mat img, ref int[] array, ref int[] delectindex, bool needselect)
        {
            Mat hirerarchy = new Mat();
            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(img, vvp, hirerarchy, RetrType.Tree, ChainApproxMethod.ChainApproxSimple);

            int[] resultarray = new int[hirerarchy.Cols * 4];
            hirerarchy.CopyTo(resultarray);
            List<int> dels = new List<int>();

            var config = Settings.Default;

            for (int i = 0; i < vvp.Size; i++)
            {
                double area = CvInvoke.ContourArea(vvp[i]);
                double length = CvInvoke.ArcLength(vvp[i], true);
                var rect = CvInvoke.MinAreaRect(vvp[i]);
                double rate = area / (rect.Size.Width * rect.Size.Height);
                bool isneed = area >= config.MinArea && area <= config.MaxArea && length >= config.MinLength && length <= config.MaxLength && rate >= config.MinRateToRect && area / length < config.MaxAreaToLength;

                if (!isneed && needselect)
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
            delectindex = dels.ToArray();
            return vvp;
        }
        static void editNode(int[] layerArray, TreeNodeCollection root)
        {
            root.Clear();
            int cnt = layerArray.Count() / 4;
            TreeNode[] s = new TreeNode[cnt];
            for (int i = 0; i <= cnt - 1; i++)
            {
                s[i] = new TreeNode(i.ToString());
            }
            for (int i = 0; i < cnt; i++)
            {
                if (layerArray[i * 4 + 3] == -1)
                {
                    root.Add(s[i]);

                }

                else
                {
                    if (layerArray[i * 4 + 3] >= 0 && !s[layerArray[i * 4 + 3]].Nodes.Contains(s[i]) && s[i].Parent == null)
                        s[layerArray[i * 4 + 3]].Nodes.Add(s[i]);
                }
            }

            for (int i = 0; i < cnt; i++)
            {
                if (layerArray[i * 4 + 2] < 0)
                {

                }

                else
                {
                    if (!s[i].Nodes.Contains(s[layerArray[i * 4 + 2]]) && s[layerArray[i * 4 + 2]].Parent == null)
                        s[i].Nodes.Add(s[layerArray[i * 4 + 2]]);
                }
            }

            for (int i = 0; i < cnt; i++)
            {
                if (layerArray[i * 4] < 0)
                {

                }

                else
                {
                    if (s[i].Parent != null)
                    {
                        s[layerArray[i * 4]].Remove();
                        int index = s[i].Index;
                        s[i].Parent.Nodes.Insert(index + 1, s[layerArray[i * 4]]);

                    }
                }
            }
            for (int i = 0; i < cnt; i++)
            {
                if (layerArray[i * 4 + 1] < 0)
                {

                }

                else
                {
                    if (s[i].Parent != null)
                    {
                        s[layerArray[i * 4 + 1]].Remove();
                        int index = s[i].Index;
                        s[i].Parent.Nodes.Insert(index, s[layerArray[i * 4 + 1]]);

                    }
                }
            }

        }

        static public VectorOfVectorOfPoint FindOutSideContours(Mat img, ref int[] indexs)
        {
            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();

            int[,] treearray = CvInvoke.FindContourTree(img, vvp, ChainApproxMethod.ChainApproxSimple);
            List<int> ls = new List<int>();
            int cnt = vvp.Size;
            var config = Settings.Default;

            for (int i = 0; i < cnt; i++)
            {
                var vp = vvp[i];
                if (!IsWriteContoursIndex(treearray, i)) continue;
                Rectangle brect = CvInvoke.BoundingRectangle(vp);
                if (brect.Height < brect.Width) continue;

                CvInvoke.ApproxPolyDP(vp, vp, config.Epsilon, true);
                double area = CvInvoke.ContourArea(vp);
                double length = CvInvoke.ArcLength(vp, true);
                if (!(area >= config.MinArea && area <= config.MaxArea && length >= config.MinLength && length <= config.MaxLength && area / length < config.MaxAreaToLength)) continue;

                var rect = CvInvoke.MinAreaRect(vp);
                double rate = area / (rect.Size.Width * rect.Size.Height);
                if (rate < config.MinRateToRect) continue;

                ls.Add(i);
            }
            indexs = ls.ToArray();
            return vvp;
        }



        static bool IsWriteContoursIndex(int[,] array, int id)
        {
            int inedx = id;
            int layer = 0;
            while (array[inedx, 3] > 0)
            {
                inedx = array[inedx, 3];
                layer++;
            }

            return layer % 2 == 0;
        }

        #endregion

        #region SvmProcess

        static internal MCvScalar getcolor(int lebel)
        {
            if (lebel >= 12)
                return new MCvScalar(127, 127, 127);
            else
            {
                int m = lebel / 3, n = lebel % 3;
                int b = 0, g = 0, r = 0;
                switch (n)
                {
                    case 0:
                        b = 256 * (4 - m) / 4 - 1;
                        g = 256 * m / 4 - 1;
                        r = 256 * m / 4 - 1;
                        break;
                    case 1:
                        g = 256 * (4 - m) / 4 - 1;
                        b = 256 * m / 4 - 1;
                        r = 256 * m / 4 - 1;
                        break;
                    case 2:
                        r = 256 * (4 - m) / 4 - 1;
                        b = 256 * m / 4 - 1;
                        g = 256 * m / 4 - 1;
                        break;
                }

                return new MCvScalar(b, g, r);
            }

        }
        static public double[] extract(Mat grayimg, int threshold = 30)
        {
            int width = grayimg.Size.Width, height = grayimg.Size.Height, step = grayimg.Step;
            int size = width * height;
            double[] result = new double[size];
            unsafe
            {
                byte* ptr = (byte*)grayimg.DataPointer;
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        result[i * step + j] = *(ptr + i * step + j) > threshold ? 1 : 0;
                    }
                }
            }

            return result;
        }
        static public Image<Gray, float> GetOneLineVector(Image<Gray, float> inputimg)
        {

            Image<Gray, float> outimg = new Image<Gray, float>(inputimg.Size.Height * inputimg.Size.Width, 1);
            unsafe
            {
                float* inputhead = (float*)inputimg.Mat.DataPointer;
                float* outputhead = (float*)outimg.Mat.DataPointer;
                int w = inputimg.Width, h = inputimg.Height, instep = inputimg.Mat.Step;

                for (int hidx = 0; hidx < h; hidx++)
                    for (int widx = 0; widx < w; widx++)
                    {
                        *(outputhead + hidx * w + widx) = *(inputhead + hidx * w + widx);
                    }
            }

            return outimg;
        }
        static public Mat SvmResult(Mat binaryimg, Dictionary<int, int> result, MulticlassSupportVectorMachine svm)
        {
            int[] indexs = null;
            result.Clear();
            Mat blackimg = new Mat();
            CvInvoke.CvtColor(binaryimg, blackimg, ColorConversion.Gray2Bgr);
            var vvp = FindOutSideContours(binaryimg, ref indexs);
            int cnt = indexs.Length;

            for (int i = 0; i < cnt; i++)
            {
                var vp = vvp[indexs[i]];
                Mat img = GetSquareExampleImg(vp, RoadTransform.ExampleSize);
                double[] array = extract(img);
                int label = svm.Compute(array, MulticlassComputeMethod.Elimination);
                result[indexs[i]] = label;
                CvInvoke.DrawContours(blackimg, vvp, indexs[i], getcolor(label), -1);
            }
            return blackimg;
        }
        #endregion

        #region MyProcessFunction

        static public VectorOfMat NormolizeHsvImg(Mat hsvimg, Mat mask = null)
        {
            VectorOfMat vm = new VectorOfMat();
            CvInvoke.Split(hsvimg, vm);
            CvInvoke.Normalize(vm[1], vm[1], 0, 255, NormType.MinMax, DepthType.Default, mask);
            CvInvoke.Normalize(vm[2], vm[2], 0, 255, NormType.MinMax, DepthType.Default, mask);
            return vm;
        }
        static public void CreatAnglesImg(string filepath, params double[] angles)
        {

            Image<Bgr, byte> img = new Image<Bgr, byte>(filepath);
            foreach (double angle in angles)
            {
                Image<Bgr, byte> rotate = img.Rotate(angle, new Bgr(0, 0, 0), true);
                rotate.Save(string.Format("{0}\\{1}_{2}{3}", Path.GetDirectoryName(filepath), Path.GetFileNameWithoutExtension(filepath), angle, Path.GetExtension(filepath)));
            }
        }
        static public Image<Bgr, byte>[] CreatAnglesImg(Image<Bgr, byte> img, params double[] angles)
        {
            int cnt = angles.Count();
            var imgs = new Image<Bgr, byte>[cnt];
            for (int i = 0; i < cnt; i++)
            {
                imgs[i] = img.Rotate(angles[i], new Bgr(0, 0, 0), true);
            }
            return imgs;
        }
        static Image<Bgr, Byte> GetImgPart(Image<Bgr, Byte> Img, Point[] pts)
        {
            Image<Gray, Byte> ImgMask = new Image<Gray, byte>(Img.Size);

            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint(new Point[1][] { pts });
            CvInvoke.DrawContours(ImgMask, vvp, -1, new MCvScalar(255), -1);
            Image<Bgr, Byte> result = Img.Copy(ImgMask);

            vvp.Dispose();
            ImgMask.Dispose();

            return result;
        }



        static public Mat MyBgrToGray(Mat img, double bk = 1, double gk = 1, double rk = 1)
        {
            if (img == null || img.IsEmpty || img.NumberOfChannels != 3) throw new AggregateException("img is unvalid");

            Mat result = new Mat(img.Size, DepthType.Cv8U, 1);
            unsafe
            {

                byte* imghead = (byte*)img.DataPointer, resulthead = (byte*)result.DataPointer;
                int rows = img.Rows, cols = img.Cols, imgstep = img.Step, resultstep = result.Step, chs = img.NumberOfChannels;
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        byte* bgrhead = imghead + i * imgstep + j * chs;
                        byte* grayhead = resulthead + i * resultstep + j;
                        double sum = bgrhead[0] * bk + bgrhead[1] * gk + bgrhead[2] * rk;
                        *grayhead = (byte)(sum / 3);
                    }
                }
            }

            return result;
        }
        static private void myByteImgAdd(Mat src, Mat added, Mat dst)
        {
            if (src == null || src.IsEmpty || src.NumberOfChannels != 1 || src.Depth != DepthType.Cv8U
            || added == null || added.IsEmpty || added.NumberOfChannels != 1 || added.Depth != DepthType.Cv8U || src.Size != added.Size)
            {
                throw new ArgumentException("Unvaild Input");
            }
            unsafe
            {
                byte* resultHead = (byte*)dst.DataPointer, srcHead = (byte*)src.DataPointer, addedHead = (byte*)added.DataPointer;
                int rows = src.Rows, cols = src.Cols;

                for (int i = 0; i < rows; i++)
                {
                    double k = (double)i / rows;
                    double k2 = (1 - k) * (1 - k);
                    for (int j = 0; j < cols; j++)
                    {
                        *(resultHead + i * cols + j) = (byte)(*(srcHead + i * cols + j) + k2 * (*(addedHead + i * cols + j)));
                    }
                }
            }

        }
        static public void EdgeEnhancement(Mat img)
        {
            if (img == null || img.IsEmpty || img.NumberOfChannels != 1) throw new ArgumentException("img is empty or not one channel");
            Mat laplace = new Mat();
            Mat gussbulr = new Mat();
            CvInvoke.GaussianBlur(img, gussbulr, new Size(3, 3), 0, 0);
            CvInvoke.Laplacian(gussbulr, laplace, DepthType.Cv16S);
            Mat laplacemat8u = new Mat();
            laplace.ConvertTo(laplacemat8u, DepthType.Cv8U);
            //CvInvoke.Add(img, laplacemat8u, img);
            myByteImgAdd(img, laplacemat8u, img);
            laplacemat8u.Dispose();
            laplace.Dispose();
            gussbulr.Dispose();

        }

        static public void MyThreshold(Mat img, byte min, byte max, byte setvalue = 255, bool isorop = false)
        {
            if (img == null || img.IsEmpty || img.NumberOfChannels != 1 || img.Depth != DepthType.Cv8U) throw new ArgumentException("img is empty or img.chanaels!=1 or img.depth isnot CV8U");
            bool needor = min > max && isorop;
            unsafe
            {
                byte* ptr = (byte*)img.DataPointer;
                int cols = img.Cols, rows = img.Rows, step = img.Step;
                for (int i = 0; i < rows; i++)
                {
                    byte* rowhead = ptr + i * step;
                    for (int j = 0; j < cols; j++)
                    {
                        byte value = *(rowhead + j);
                        bool cond1 = value >= min, cond2 = value <= max;
                        bool isinrange = needor ? cond1 || cond2 : cond1 && cond2;
                        if (isinrange)
                        {
                            *(rowhead + j) = setvalue;
                        }
                        else
                        {
                            *(rowhead + j) = 0;
                        }
                    }
                }
            }


        }
        static public Mat HsvThreshold(Mat img, double hmin, double smin, double vmin, double hmax = 180, double smax = 255, double vmax = 255, bool doNormalize = true)
        {
            if (img == null || img.IsEmpty) return null;

            Mat hsv = new Mat();
            CvInvoke.CvtColor(img, hsv, ColorConversion.Bgr2Hsv);
            var vm = new VectorOfMat();
            CvInvoke.Split(hsv, vm);
            hsv.Dispose();

            if (doNormalize)
            {
                CvInvoke.Normalize(vm[2], vm[2], 0, 255, NormType.MinMax, DepthType.Default);
            }

            MyThreshold(vm[0], (byte)hmin, (byte)hmax, 255, true);

            MyThreshold(vm[1], (byte)smin, (byte)smax, 255, false);

            MyThreshold(vm[2], (byte)vmin, (byte)vmax, 255, false);

            Mat mask = new Mat(img.Size, DepthType.Cv8U, 1);
            mask.SetTo(new MCvScalar(255));
            CvInvoke.BitwiseAnd(mask, vm[0], mask);
            CvInvoke.BitwiseAnd(mask, vm[1], mask);
            CvInvoke.BitwiseAnd(mask, vm[2], mask);
            Mat result = new Mat(img.Size, DepthType.Cv8U, 3);
            result.SetTo(new MCvScalar(0));
            img.CopyTo(result, mask);
            vm.Dispose();
            return result;
        }

        static public void MyMinMaxNormalize(Mat img, double min, double max)
        {
            if (img == null || img.IsEmpty || img.NumberOfChannels != 1 || img.Depth != DepthType.Cv8U) throw new ArgumentException("img is unvalid");
            Mat result = new Mat(img.Size, DepthType.Cv8U, 1);
            unsafe
            {
                byte* imghead = (byte*)img.DataPointer;
                int rows = img.Rows, cols = img.Cols, step = img.Step;
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        byte* address = imghead + i * step + j;
                        double value = *address;
                        if (value >= max) value = 255;
                        else if (value <= min) value = 0;
                        else value = (value - min) / (max - min) * 255;
                        *address = (byte)value;
                    }
                }
            }
        }

        static public Mat CalTransformatMat(Size img, float xk, float yk, float ltk, int outwidth, int outheight)
        {
            PointF[] srcTri = new PointF[4];
            PointF[] dstTri = new PointF[4];
            int inw = img.Width;
            int inh = img.Height;
            srcTri[0] = new PointF(xk * inw, yk * inh);
            srcTri[1] = new PointF(inw - xk * inw, yk * inh);
            srcTri[2] = new PointF(ltk * inw + (inw / 2 + 1), inh - 1);
            srcTri[3] = new PointF((inw / 2) - ltk * inw, inh - 1);
            dstTri[0] = new PointF(0, 0);
            dstTri[1] = new PointF(outwidth - 1, 0);
            dstTri[2] = new PointF(outwidth - 1, outheight - 1);
            dstTri[3] = new PointF(0, outheight - 1);
            return CvInvoke.GetPerspectiveTransform(srcTri, dstTri);
        }
        #endregion

        #region Math

        static internal bool rangeEquals(double a, double b, double errorvalue = 0.05)
        {
            return Math.Abs(a - b) / b <= errorvalue;
        }
        static public bool ArraySmooth(double[] value, int rangeLen, double e, ref double[] value2)
        {
            if (value == null || rangeLen <= 0 || value.Count() < (rangeLen * 2 + 1)) return false;
            int cnt = value.Count();
            uint Num = (uint)rangeLen;

            if (null == value2 || value2.Length != value2.Length) value2 = new double[cnt];

            if (1 == rangeLen)
            {
                Array.Copy(value, value2, value.Length);
                return true;
            }

            for (int i = 0; i < cnt; i++)
            {
                if (e > 0 && System.Math.Abs(value[i]) > e) { value2[i] = value[i]; continue; }

                double dSum = value[i];
                int iSCnt = 1;

                int t0 = System.Math.Max(i - rangeLen, 0);
                int t1 = System.Math.Min(i + rangeLen, cnt - 1);

                for (int t = i - 1; t >= t0; --t)
                {
                    if (e > 0 && System.Math.Abs(value[t]) > e) break;

                    dSum += value[t];
                    iSCnt += 1;
                }

                for (int t = i + 1; t <= t1; ++t)
                {
                    if (e > 0 && System.Math.Abs(value[t]) > e) break;

                    dSum += value[t];
                    iSCnt += 1;
                }

                if (1 == iSCnt)
                {
                    value2[i] = value[i];
                }
                else
                {
                    double v = dSum / iSCnt;
                    double v0 = System.Math.Abs(value[i]);
                    double v1 = System.Math.Abs(v);

                    //if (v0!=0 && (v0 < v1 * 0.1 || v0 > v1 * 10)) value2[i] = value[i];
                    //else value2[i] = v;
                    value2[i] = v;
                }

            }
            return true;
        }
        static public IEnumerable<int> GetTopIndexs(params int[] list)
        {
            int cnt = list.Count();
            List<int> indexs = new List<int>();
            for (int i = 1; i <= cnt - 2; i++)
                if ((list[i] - list[i - 1]) * (list[i] - list[i + 1]) > 0)
                    indexs.Add(i);
            return indexs;
        }

        #endregion

        #region RoadDetect

        static public LineSegment2D[] LaneDetect(Mat img)
        {
            var config = Settings.Default;
            if (img == null || img.IsEmpty || img.NumberOfChannels != 1) throw new ArgumentException("Img is Unvalid");
            Mat edge = new Mat();
            CvInvoke.Canny(img, edge, config.CannyThreshold, config.CannyLink);
            return CvInvoke.HoughLinesP(edge, config.LineRho, config.LineTheta, config.LineThreshold, config.LineMinLength, config.LineMaxGrap);
        }

        static public double compare(LineSegment2D ln1)
        {
            int h = Settings.Default.OH;

            if (ln1.Direction.Y == 0) return double.MinValue;
            else
            {
                return (h - 1 - ln1.P1.Y) / ln1.Direction.Y * ln1.Direction.X + ln1.P1.X;
            }
        }

        static Comparison<LineSegment2D> comparefun = (LineSegment2D ln1, LineSegment2D ln2) =>
        {
            double ln1x = compare(ln1);
            double ln2x = compare(ln2);
            if (ln1x == ln2x) return 0;
            else if (ln1x < ln2x) return -1;
            else return 1;
        };

        static public PointF GetPoint(LineSegment2D ln1, LineSegment2D ln2)
        {
            float a = ln1.Direction.Y, b = ln1.Direction.X, m = ln1.Direction.Y * ln1.P1.X - ln1.Direction.X * ln1.P1.Y;
            float c = ln2.Direction.Y, d = ln2.Direction.X, n = ln2.Direction.Y * ln2.P1.X - ln2.Direction.X * ln2.P1.Y;
            float x = (m * d - b * n) / (a * d - b * c);
            float y = (m * c - a * n) / (b * c - a * d);
            return new PointF(x, y);

        }

        static public LineSegment2D[] SelectLines(LineSegment2D[] lines, int errorangle = 5, int exnums = 2)
        {
            int referencedis = 0;
            int spindex = (Settings.Default.OW - 1) / 2; ;
            Array.Sort(lines, comparefun);
            int lastindex = -1;
            double lastx = double.MinValue;
            int cnt = lines.Count();
            for (int i = 0; i < cnt; i++)
            {
                var line = lines[i];

                double x = compare(lines[i]);
                if (x > spindex && lastx < spindex && i >= 1 && lastindex != -1)
                {
                    for (int j = i; j < cnt; j++)
                    {
                        var lnj = lines[j];
                        if (lnj.Length < Settings.Default.OH / 4 || Math.Max(lnj.P1.Y, lnj.P2.Y) < Settings.Default.OH / 3 * 2) continue;
                        for (int k = lastindex; k >= 0; k--)
                        {

                            var lnk = lines[k];
                            if (lnk.Length < Settings.Default.OH / 4 || Math.Max(lnk.P1.Y, lnk.P2.Y) < Settings.Default.OH / 3 * 2) continue;

                            double jx = compare(lnj);
                            double kx = compare(lnk);

                            double angle = Math.Abs(lnj.GetExteriorAngleDegree(lnk));
                            if (angle > 90)
                                angle = 180 - angle;
                            if (Math.Abs(jx - kx) > Settings.Default.OW / 4 && angle < errorangle)
                                return new LineSegment2D[] { lines[k], lines[j] };
                        }
                    }
                }
                lastx = x;
                lastindex = i;

            }

            return new LineSegment2D[0];
        }

        static public void DrawMiddlePos(Mat img, LineSegment2D[] lines, double pos = 0.8)
        {
            Mat black = new Mat(img.Size, DepthType.Cv8U, 3);
            black.SetTo(default(MCvScalar));

            MCvScalar col = new MCvScalar(0, 255, 255);
            MCvScalar pcol = new MCvScalar(100, 100, 255);
            int middleindex = (img.Width - 1) / 2;
            int verticalvalue = (int)(img.Height * 0.95) - 1;
            int verticalvalue2 = 0;
            //Point p1 = new Point(middleindex, verticalvalue - 10);
            //Point p2 = new Point(middleindex, verticalvalue + 10);
            Point cammid = new Point(middleindex, verticalvalue);
            //CvInvoke.Line(img, p1, p2, col, 3);
            if (lines.Count() != 2) return;
            Point lp = new Point((int)((double)(verticalvalue - lines[0].P1.Y) / lines[0].Direction.Y * lines[0].Direction.X) + lines[0].P1.X, verticalvalue);
            Point rp = new Point((int)((double)(verticalvalue - lines[1].P1.Y) / lines[1].Direction.Y * lines[1].Direction.X) + lines[1].P1.X, verticalvalue);
            Point lp2 = new Point((int)((double)(verticalvalue2 - lines[0].P1.Y) / lines[0].Direction.Y * lines[0].Direction.X) + lines[0].P1.X, verticalvalue2);
            Point rp2 = new Point((int)((double)(verticalvalue2 - lines[1].P1.Y) / lines[1].Direction.Y * lines[1].Direction.X) + lines[1].P1.X, verticalvalue2);
            Point midp = new Point((lp.X + rp.X) / 2, verticalvalue);
            Point midp2 = new Point((lp2.X + rp2.X) / 2, verticalvalue2);
            var vvp = new VectorOfVectorOfPoint(new Point[][] { new Point[] { lp, rp, rp2, lp2, } });
            CvInvoke.DrawContours(black, vvp, -1, new MCvScalar(0, 50, 0), -1);
            CvInvoke.Line(black, midp, midp2, col, 2);
            CvInvoke.ArrowedLine(black, midp, cammid, col, 2);
            CvInvoke.Circle(black, lp, 2, pcol, -1);
            CvInvoke.Circle(black, rp, 2, pcol, -1);
            CvInvoke.Circle(black, midp, 2, pcol, -1);
            CvInvoke.PutText(black, string.Format("{0}%", (int)((double)(middleindex - midp.X) / (rp.X - lp.X) * 100)), midp, FontFace.HersheyComplex, 0.5, col, 1);
            Mat mask = new Mat();
            CvInvoke.CvtColor(black, mask, ColorConversion.Bgr2Gray);
            MyAddWeight(img, black, 0.5, mask);
        }

        static public void MyAddWeight(Mat addedimg, Mat addtoimg, double weight = 0.5, Mat mask = null)
        {
            if (mask == null)
            {
                mask = new Mat(addedimg.Size, DepthType.Cv8U, 1);
                mask.SetTo(new MCvScalar(255));
            }

            unsafe
            {
                byte* addedhead = (byte*)addedimg.DataPointer, addtohead = (byte*)addtoimg.DataPointer, maskhead = (byte*)mask.DataPointer;
                int imgw = addedimg.Cols, imgh = addedimg.Rows, imgstep = addedimg.Step, addtostep = addtoimg.Step;
                int maskstep = mask.Step;
                for (int i = 0; i < imgh; i++)
                {
                    for (int j = 0; j < imgw; j++)
                    {
                        if (*(maskhead + i * maskstep + j) > 0)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                byte* addedp = addedhead + i * imgstep + j * 3 + k;
                                *addedp = (byte)((1 - weight) * (*addedp) + weight * (*(addtohead + i * addtostep + j * 3 + k)));
                            }
                        }
                    }
                }

            }

        }

        static public Mat RoadLineDetect(Mat img, int range = 10, byte diff = 20, bool isHorizontal = true)
        {
            if (img.IsEmpty) return null;
            Mat result = new Mat(img.Size, DepthType.Cv8U, 1);
            unsafe
            {
                byte* head = (byte*)img.DataPointer, resulthead = (byte*)result.DataPointer;
                int cols = img.Cols, rows = img.Rows, step = img.Step;
                if (isHorizontal)
                {
                    for (int i = 0; i < rows; i++)
                    {

                        byte* rowhead = head + i * step;
                        for (int j = 0; j < cols; j++)
                        {
                            int leftcols = j - range > 0 ? j - range : 0;
                            int rightcols = j + range > cols ? cols - 1 : j + range;
                            byte value = *(rowhead + j);
                            if (*(rowhead + leftcols) < value - diff && *(rowhead + rightcols) < value - diff)
                            {
                                *(resulthead + i * step + j) = 255;
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < cols; i++)
                    {

                        byte* rowhead = head + i;
                        for (int j = 0; j < rows; j++)
                        {
                            int leftcols = j - range > 0 ? j - range : 0;
                            int rightcols = j + range > rows ? rows - 1 : j + range;
                            byte value = *(rowhead + j * step);
                            if (*(rowhead + leftcols * step) < value - diff && *(rowhead + rightcols * step) < value - diff)
                            {
                                *(resulthead + j * step + i) = 255;
                            }
                        }
                    }

                }

            }
            return result;
        }

        static public void RoadPreProcess(Mat inputimg, ref double mingray, ref double maxgray, IInputArray mask = null, bool is_origin = false, bool useopen = true, int openSize = 2, bool usemeadin = true, int meadinSize = 2)
        {
            var config = Settings.Default;

            Mat intmat = new Mat();
            Mat rect = null;
            if (is_origin)
                rect = new Mat(inputimg, new Rectangle(new Point(inputimg.Width / 3, (int)(inputimg.Height * RoadTransform.AY)), new Size(inputimg.Width / 3, (int)(inputimg.Height * (1 - RoadTransform.AY)))));
            else
                rect = new Mat(inputimg, new Rectangle(new Point(inputimg.Width / 6, inputimg.Height / 4), new Size(inputimg.Width / 3 * 2, inputimg.Height / 2)));

            int[] maxid = new int[2], minid = new int[2];
            CvInvoke.MinMaxIdx(rect, out mingray, out maxgray, minid, maxid);
            MyMinMaxNormalize(inputimg, mingray, maxgray);
            CvInvoke.MedianBlur(inputimg, inputimg, meadinSize * 2 + 1);
            CvInvoke.MorphologyEx(inputimg, inputimg, MorphOp.Open, CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, openSize * 2 + 1), new Point(-1, -1)), new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
        }
        static public Mat GetLine(IInputArray inputimg)
        {
            var lineimg = new Mat();
            var config = Settings.Default;
            CvInvoke.AdaptiveThreshold(inputimg, lineimg, 255, AdaptiveThresholdType.MeanC, ThresholdType.Binary, 2 * config.AdaptiveBlockSize + 1, config.AdaptiveParam);
            return lineimg;
        }
        static public Point FindSeedPointToFill(Mat img, int threshold = 150, double upos = 0.5, double vpos = 0.5)
        {
            if (img.Depth != DepthType.Cv8U || img.NumberOfChannels != 1) throw new ArgumentException("img.Depth!= DepthType.Cv8U||img.NumberOfChannels!=1");
            Point p = new Point((int)(img.Size.Width * upos), (int)(img.Size.Height * vpos));
            int distance = img.Size.Width / 20, tims = 0;
            int rows = img.Rows, cols = img.Cols;
            unsafe
            {
                byte* ptr = (byte*)img.DataPointer;
                while (*(ptr + p.Y * cols + p.X) > threshold)
                {
                    tims++;
                    p.X += (tims % 2 == 1 ? -1 : 1) * distance;
                    distance += 5;
                    if (p.X < 0 || p.X > img.Cols - 1)
                        break;
                }

                if (p.X < 0 || p.X >= cols)
                {
                    p = new Point((int)(img.Size.Width * upos), (int)(img.Size.Height * vpos));
                }
            }
            return p;
        }
        /// <summary>
        /// 基于跨越的行扫描算法：检测人行道区域
        /// </summary>
        /// <param name="binaryimg">包含人行道的二值图</param>
        /// <param name="rowstep">横向扫描跳跃行数</param>
        /// <param name="minrownums">最小行数要求</param>
        /// <param name="changePointsNumThreshold">当行跳变条点数要求</param>
        /// <param name="minwritertoblack">最小白黑比</param>
        /// <param name="maxwritertoblack">最大白黑比</param>
        /// <param name="maxerrorstep">最大容忍误检次数</param>
        /// <param name="minAreaRate">最小占面比</param>
        /// <returns></returns>
        static public VectorOfVectorOfPoint WalkRoadImg(Mat binaryimg, int rowstep = 4, int minrownums = 20, int changePointsNumThreshold = 5, double minwritertoblack = 0.4, double maxwritertoblack = 2, int maxerrorstep = 4, double minAreaRate = 0.05)
        {
            changePointsNumThreshold = 7;
            if (binaryimg.NumberOfChannels != 1) return null;
            CvInvoke.Threshold(binaryimg, binaryimg, 130, 255, ThresholdType.Binary);
            VectorOfVectorOfPoint vvp = new VectorOfVectorOfPoint();
            List<Point> left = new List<Point>();
            List<Point> right = new List<Point>();
            int rows = binaryimg.Rows, colums = binaryimg.Cols;
            unsafe
            {
                byte* ptr = (byte*)binaryimg.DataPointer;
                int beginRowIndex = 0, endRowIndex = 0, curlostnum = 0, step = binaryimg.Step;
                bool find = false;

                for (int i = 0; i < rows; i += rowstep)
                {
                    byte* rowhead = (ptr + i * step);
                    bool isBeginAtBlack = *rowhead == 0;

                    int blackwidthsum = 0, writewidthsum = 0, blacknum = 0, writenum = 0;
                    int lastchangeindex = 0;
                    int beginIndex = 0, endIndex = 0;

                    for (int j = 1; j < colums; j++)
                    {
                        byte curvalue = *(rowhead + j), prevvalue = *(rowhead + j - 1); ;
                        if ((curvalue * prevvalue == 0) && curvalue != prevvalue)
                        {
                            if (curvalue > 0)
                            {
                                if (lastchangeindex > 0)
                                {
                                    int backdis = (j - lastchangeindex);
                                    if (blacknum > 0 && backdis > blackwidthsum / blacknum * 3 && lastchangeindex > colums / 4 && writenum + blacknum > changePointsNumThreshold)
                                    {
                                        endIndex = lastchangeindex;
                                        break;
                                    }

                                    bool isclearprev = false;

                                    if (backdis > colums / 4 && backdis > lastchangeindex * 2 && lastchangeindex < colums / 4)
                                    {
                                        blackwidthsum = 0;
                                        writewidthsum = 0;
                                        blacknum = 0;
                                        writenum = 0;
                                        isclearprev = true;
                                        beginIndex = j;
                                        endIndex = 0;

                                    }

                                    if (isclearprev)
                                    {
                                        lastchangeindex = j;
                                    }
                                    else
                                    {
                                        blackwidthsum += (j - lastchangeindex);
                                        blacknum++;
                                        lastchangeindex = j;
                                    }


                                }
                                else
                                {
                                    beginIndex = j;
                                    lastchangeindex = j;
                                }



                            }
                            else
                            {
                                writewidthsum += (j - lastchangeindex);
                                writenum++;
                                lastchangeindex = j;

                            }
                        }
                        if (j == colums - 1)
                        {
                            endIndex = curvalue > 0 ? colums - 1 : lastchangeindex;
                        }
                    }
                    bool isleagal = false;
                    if (writenum != 0 && blacknum != 0)
                    {
                        double rate = ((double)writewidthsum / writenum) / ((double)blackwidthsum / blacknum);
                        if (rate > minwritertoblack && rate < maxwritertoblack && blacknum + writenum >= changePointsNumThreshold)
                        {
                            isleagal = true;
                            if (!find)
                            {
                                beginRowIndex = i;
                                find = true;
                                endRowIndex = i;
                            }
                            else
                            {
                                endRowIndex = i;
                            }

                            Point p1 = new Point(beginIndex, i);
                            left.Add(p1);
                            Point p2 = new Point(endIndex, i);
                            right.Add(p2);
                            curlostnum = 0;
                        }
                    }

                    if (!isleagal)
                    {
                        //到达不满足条件的行时
                        if (endRowIndex > 0)
                        {
                            //累加误行数量
                            curlostnum++;
                        }

                        //误行达到一定数量时被认为一个人行道对象识别结束
                        if (curlostnum >= maxerrorstep)
                        {
                            //如果行数达到要求 则该对象加入到结果中
                            if (endRowIndex - beginRowIndex > minrownums)
                            {
                                VectorOfPoint vp = new VectorOfPoint(left.ToArray());
                                right.Reverse();
                                vp.Push(right.ToArray());
                                double area = CvInvoke.ContourArea(vp);
                                Rectangle ro = CvInvoke.BoundingRectangle(vp);
                                double wh = (double)ro.Width / ro.Height;
                                bool canadded = (double)(ro.Size.Height * ro.Size.Width) / (colums * rows) > minAreaRate && wh > 1.5 && wh < 6;
                                if (canadded)
                                {
                                    vvp.Push(vp);
                                }
                                else
                                {
                                    vp.Dispose();
                                }



                            }
                            //重置局部变量
                            beginRowIndex = 0; endRowIndex = 0;
                            curlostnum = 0;
                            find = false;
                            right.Clear();
                            left.Clear();
                        }

                    }
                }

                if (find)
                {
                    if (endRowIndex - beginRowIndex > minrownums)
                    {
                        VectorOfPoint vp = new VectorOfPoint(left.ToArray());
                        right.Reverse();
                        vp.Push(right.ToArray());
                        vvp.Push(vp);
                    }
                }
            }

            return vvp;
        }
        static public void FillRoad(Mat roadGray, MCvScalar color = default(MCvScalar), int threshold = 150, int lowdiff = 4, int highdiff = 3)
        {
            Mat imgresize = new Mat();
            CvInvoke.Resize(roadGray, imgresize, new Size(roadGray.Width / 2, roadGray.Height / 2));


            Rectangle rect = new Rectangle();
            Point p1 = FindSeedPointToFill(imgresize, threshold, 0.5, 0.5);
            int nums = CvInvoke.FloodFill(imgresize, null, p1, color, out rect, new MCvScalar(lowdiff), new MCvScalar(highdiff), Connectivity.FourConnected, FloodFillType.Default);
            Mat recover = new Mat();
            CvInvoke.Resize(imgresize, recover, new Size(roadGray.Width, roadGray.Height));
            imgresize.Dispose();
            recover.CopyTo(roadGray);
            recover.Dispose();

        }
        static public Mat FinalLineProcess(Mat img, out long time, bool needtrans = false, bool findRoadArea = true, bool hascolor = false, Mat mask = null)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var imgs = img.Split();
            imgs[0].Dispose();
            imgs[1].Dispose();
            Mat vch = imgs[2];

            Mat grayclone = vch.Clone();
            double min = 0, max = 0;
            RoadPreProcess(vch, ref min, ref max, null, true, true, 2, false, 2);

            Mat processimg = vch;

            Mat unroad = null;
            Mat trans = null;
            Mat blackRoad = null;
            if (needtrans && !RoadTransform.TransformMat.IsEmpty)
            {
                unroad = RoadTransform.RoadMask.Clone();
                trans = RoadTransform.WarpPerspective(processimg);
            }
            else
            {
                trans = processimg.Clone();
                unroad = new Mat(imgs[2].Size, DepthType.Cv8U, 1);
                unroad.SetTo(new MCvScalar(255));
            }

            Mat fillarea = new Mat(grayclone, new Rectangle(new Point(0, (int)(processimg.Height * RoadTransform.AY)), new Size(processimg.Width, (int)(processimg.Height * (1 - RoadTransform.AY)))));
            int diff = (int)(max - min) / 50;
            FillRoad(fillarea, new MCvScalar(10), diff, diff);

            if (needtrans && !RoadTransform.TransformMat.IsEmpty)
            {
                blackRoad = RoadTransform.WarpPerspective(processimg);
                processimg.Dispose();
            }
            else
            {
                blackRoad = processimg;
            }

            var line = GetLine(trans);
            MyThreshold(blackRoad, 10, 10, 127);
            CvInvoke.MorphologyEx(blackRoad, blackRoad, MorphOp.Close, Struct, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0));
            CvInvoke.Add(line, blackRoad, line);
            CvInvoke.BitwiseAnd(line, unroad, line);
            vch.Dispose();
            unroad.Dispose();
            trans.Dispose();
            blackRoad.Dispose();
            sw.Stop();
            time = sw.ElapsedMilliseconds;

            if (hascolor)
            {
                Mat bgrresult = new Mat();
                img.CopyTo(bgrresult, line);
                line.Dispose();
                return bgrresult;
            }
            else
            {
                return line;
            }
        }
        static public Mat SpeedProcess(Mat img, out long time, out LineSegment2D[] lines, bool findRoadArea = true, Mat mask = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            //var imgs = img.Split();
            //imgs[0].Dispose();
            //imgs[1].Dispose();
            Mat vch = OpencvMath.MyBgrToGray(img);



            Mat trans = RoadTransform.WarpPerspective(vch);
            Mat unroad = RoadTransform.RoadMask;
            LineSegment2D[] ls = OpencvMath.LaneDetect(trans);
            lines = OpencvMath.SelectLines(ls);

            double min = 0, max = 0;
            RoadPreProcess(trans, ref min, ref max, null, false, true, 2, true, 2);
            var line = GetLine(trans);
            if (findRoadArea)
            {
                Mat rect = new Mat(vch, new Rectangle(new Point(0, (int)(vch.Height * RoadTransform.AY)), new Size(vch.Width, (int)(vch.Height * (1 - RoadTransform.AY)))));
                int diff = (int)(max - min) / 50;
                FillRoad(rect, new MCvScalar(0), (int)(max + min) / 3 * 2, diff, diff);
                Mat transtresult = RoadTransform.WarpPerspective(vch);
                CvInvoke.Threshold(transtresult, transtresult, 1, 255, ThresholdType.Binary);
                CvInvoke.MorphologyEx(transtresult, transtresult, MorphOp.Open, Struct, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));
                CvInvoke.BitwiseAnd(line, transtresult, line);
                MyThreshold(transtresult, 0, 0, 127);
                CvInvoke.Add(line, transtresult, line);
                transtresult.Dispose();
                vch.Dispose();
            }

            CvInvoke.BitwiseAnd(line, unroad, line);
            //CvInvoke.MorphologyEx(line, line, MorphOp.Close, Struct, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));
            //CvInvoke.MorphologyEx(line, line, MorphOp.Open, Struct, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));
            if (mask != null) { CvInvoke.BitwiseAnd(line, mask, line); }

            sw.Stop();
            time = sw.ElapsedMilliseconds;

            return line;
        }
        static public Mat SpeedProcessNoWarp(Mat img, out long time, Mat mask = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Mat grayimg = null;
            if (img.NumberOfChannels == 3)
            {
                grayimg = OpencvMath.MyBgrToGray(img);
            }
            else
            {
                grayimg = null;
            }

            double min = 0, max = 0;
            RoadPreProcess(grayimg, ref min, ref max, null, false, true, 2, true, 2);
            var line = GetLine(grayimg);
            CvInvoke.BitwiseAnd(line, RoadTransform.RoadMask, line);

            sw.Stop();
            time = sw.ElapsedMilliseconds;
            return line;
        }

        static public Mat MyHorizontalCanny(Mat grayimg, int thresholdhigh = 40 , int thresholdlow = 20, int type = 1) {
            if (null == grayimg || grayimg.IsEmpty || grayimg.Depth != DepthType.Cv8U || grayimg.NumberOfChannels != 1) throw new ArgumentException("img is valid!");
            int row = grayimg.Rows, col = grayimg.Cols, step = grayimg.Step;
            Mat img = new Mat();
            CvInvoke.GaussianBlur(grayimg, img, new Size(3, 3), 0, 0);
            Mat result = new Mat(grayimg.Size, DepthType.Cv8U, 1);
            Mat maxresult = new Mat(grayimg.Size, DepthType.Cv8U, 1);
            result.SetTo(default(MCvScalar));
            maxresult.SetTo(default(MCvScalar));
            unsafe
            {
                byte* dataptr = (byte*)img.DataPointer;
                int rstep = result.Step;
                byte* rdataptr = (byte*)result.DataPointer;
                byte* mrdataptr = (byte*)maxresult.DataPointer;
                for (int i = 0; i < row; i++)
                {
                    byte diff = 0;
                    double ys = (double)i / (row - 1);
                    if (type == -1)
                    {
                        diff = (byte)Math.Sqrt( ys * thresholdhigh * thresholdhigh + (1 - ys) * thresholdlow * thresholdlow);
                    }else if (type == 1)
                    {
                        diff = (byte)Math.Pow(ys * Math.Sqrt(thresholdhigh) + (1 - ys) * Math.Sqrt(thresholdlow),2);
                    }else
                    {
                        diff = (byte)(ys * thresholdhigh + (1 - ys) * thresholdlow);
                    }

                    for (int j = 1;j< col ; j++)
                    {
                        int index = i * step + j;
                        int rindex = i * rstep + j;
                        int curdiff = dataptr[index] - dataptr[index - 1];
                        if (curdiff > diff|| curdiff < -diff)
                        {
                            rdataptr[rindex] = (byte)curdiff;
                        }
                        
                    }
                }

                for (int i = 0; i < row; i++)
                {
                    for (int j = 0; j < col-1; j++)
                    {
                        int rindex = i * rstep + j;
                        if (rdataptr[rindex] > rdataptr[rindex - 1] && rdataptr[rindex] > rdataptr[rindex + 1])
                            mrdataptr[rindex] = 255;
                    }
                }
                img.Dispose();
                result.Dispose();
                return maxresult;
            }


        }
        #endregion

    }
    static public class RoadTransform
    {

        #region SaveImgOption
        public static Size ExampleSize = new Size(32, 32);
        #endregion

        #region transfrom
        private static float m_AX = 0.25f;
        private static float m_AY = 0.50f;
        private static float m_LT = 0.50f;

        private static Mat m_transformMat = new Mat();
        private static Mat m_roadMask = new Mat();
        private static Mat m_tranforMatInv = new Mat();

        public static Mat RoadMask
        {
            get
            {
                return m_roadMask;
            }
        }

        [DefaultValue(typeof(Size))]
        public static Size InputSize
        {
            get;
            set;
        }

        [DefaultValue(typeof(Size))]
        public static Size OutSize
        {
            get;
            set;
        }

        public static Mat TransformMat
        {
            get
            {
                return m_transformMat;
            }
        }

        public static float AX
        {
            get
            {
                return m_AX;
            }
        }

        public static float AY
        {
            get
            {
                return m_AY;
            }
        }

        public static float LT
        {
            get { return m_LT; }
        }

        public static Mat TranforMatInv
        {
            get
            {
                return m_tranforMatInv;
            }
        }

        public static void SetTransform(Size inputsize, float ax, float ay, float lt, int ow, int oh)
        {
            if (inputsize == default(Size)) return;

            InputSize = inputsize;
            OutSize = new Size(ow, oh);
            m_AX = ax; m_AY = ay; m_LT = lt;
            m_transformMat = OpencvMath.CalTransformatMat(InputSize, ax, ay, lt, ow, oh);
            if (m_roadMask != null)
            {
                m_roadMask.Dispose();
            }
            m_roadMask = new Mat(InputSize, DepthType.Cv8U, 1);
            m_roadMask.SetTo(new MCvScalar(255));
            CvInvoke.WarpPerspective(m_roadMask, m_roadMask, m_transformMat, OutSize);
            CvInvoke.Invert(m_transformMat, m_tranforMatInv, DecompMethod.LU);
        }
        public static void LoadSetting()
        {
            var config = Settings.Default;
            SetTransform(config.DetectArea.Size, config.AX, config.AY, config.LT, config.OW, config.OH);

        }
        public static Mat WarpPerspective(Mat img)
        {
            Mat result = new Mat();
            CvInvoke.WarpPerspective(img, result, TransformMat, OutSize, Inter.Area, Warp.Default, BorderType.Reflect);
            return result;
        }
        #endregion

    }
    static public class TestClass
    {
        static public void PtrTest()
        {

            Image<Bgr, byte> imgimg = new Image<Bgr, byte>(new Size(500, 500));
            imgimg[0, 0] = new Bgr(0, 0, 0);
            imgimg[0, 1] = new Bgr(1, 11, 111);
            imgimg[1, 0] = new Bgr(2, 22, 222);
            imgimg[1, 1] = new Bgr(3, 33, 250);


            Stopwatch Watch = new Stopwatch();
            Watch.Start();
            var result2 = imgimg.Resize(250000, 1, Inter.Linear);
            Watch.Stop();
            long time1 = Watch.ElapsedMilliseconds;

            Stopwatch Watch1 = new Stopwatch();
            Watch1.Start();
            var img2 = imgimg.Convert<Gray, float>();
            Watch1.Stop();
            long time2 = Watch1.ElapsedMilliseconds;

            Stopwatch Watch2 = new Stopwatch();
            Watch2.Start();
            var result = OpencvMath.GetOneLineVector(img2);
            Watch2.Stop();
            long time3 = Watch2.ElapsedMilliseconds;



            Mat img = imgimg.Mat;
            int w = img.Width, h = img.Width, step = img.Step, chs = img.NumberOfChannels;
            unsafe
            {
                byte* pt = (byte*)img.DataPointer;
                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        byte* value = pt + step * i + j * chs;
                        for (int k = 0; k < chs; k++)
                        {
                            byte onevalue = *(value + k);
                            //TODO:
                        }
                    }
                }
            }


        }
        /// <summary>
        /// 遍历模板函数
        /// </summary>
        /// <param name="img">Mat类型的图像，如果是Image<,>类型图像， 使用img,Mat</param>
        static public void PtrProcess(Mat img)
        {
            ///要使用指针遍历需要预先知道Mat矩阵元素的类型，通常使用的是Cv8U(unchar) 在C#里面对应byte,其他类型的对应关系见DepthType的枚举注释
            if (img == null || img.IsEmpty || img.Depth != DepthType.Cv8U) throw new ArgumentException("img is unvalid!");

            int rows = img.Rows, cols = img.Cols, step = img.Step;
           
            unsafe
            {
                byte* dataptr = (byte*)img.DataPointer;
                ///单通道图像遍历方式
                if (img.NumberOfChannels == 1)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            int index = i * step + j;
                            ///get
                            byte getvalue = dataptr[index];
                            ///set
                            byte setvalue = 127;
                            dataptr[index] = setvalue;
                        }
                    }
                }
                ///多通道图像遍历方式，以BGR图像为例
                else
                {
                    int chns = img.NumberOfChannels;
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            ///B,G,R 顺序 c= 0,1,2;
                            for (int c = 0; c < chns; c++)
                            {
                                int index = i * step + j * chns + c;
                                ///get
                                byte getvalue = dataptr[index];
                                ///set
                                byte setvalue = 127;
                                dataptr[index] = setvalue;

                            }
                        }
                    }
                }

            }


        }

        static public RoadObjectType JugdeLineShape(VectorOfPoint vp, Size imgsize)
        {
            var config = Settings.Default;
            double amax = config.MaxArea, amin = config.MinArea;
            double lmax = config.MaxLength, lmin = config.MinLength;
            double conarea = CvInvoke.ContourArea(vp);
            double length = CvInvoke.ArcLength(vp, true);
            double thiswidth = conarea / length;
            double thisheight = length / 2 * 0.9;
            var rect = CvInvoke.MinAreaRect(vp);
            double rectwidth = Math.Min(rect.Size.Width, rect.Size.Height);
            double rectheight = rect.Size.Width + rect.Size.Height;
            double areaRateToRect = conarea / rect.Size.Height / rect.Size.Width;

            if (conarea > amax || conarea < amin || length > lmax || length < lmin) return RoadObjectType.Unkown;

            if (OpencvMath.rangeEquals(thisheight, rectheight, 0.2) && OpencvMath.rangeEquals(rectwidth, rectwidth, 0.2))
            {
                if (thiswidth < imgsize.Width * 0.05)
                    return RoadObjectType.FullLine;
                else
                    return RoadObjectType.Unkown;
            }
            double w_h = rect.Size.Width / rect.Size.Height;
            double k = w_h > 1 ? w_h : 1 / w_h;

            if (areaRateToRect > 0.6 && k > 5) return RoadObjectType.PartOfDottedLine;
            else return RoadObjectType.SignInLoad;

        }

        static public Mat JugdeTest(VectorOfVectorOfPoint cons, Mat back, ref long time)
        {

            Mat test = back.Clone();
            time = 0;
            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < cons.Size; i++)
            {

                var vp = cons[i];
                if (vp == null || vp.Size == 0) continue;

                sw.Start();
                var type = JugdeLineShape(vp, back.Size);
                sw.Stop();
                time += sw.ElapsedMilliseconds;

                MCvScalar ms = new MCvScalar();
                switch (type)
                {
                    case RoadObjectType.FullLine:
                        ms = new MCvScalar(255, 255, 0);
                        break;
                    case RoadObjectType.PartOfDottedLine:
                        ms = new MCvScalar(0, 255, 0);
                        break;
                    case RoadObjectType.SignInLoad:
                        ms = new MCvScalar(255, 0, 0);
                        break;
                    case RoadObjectType.Unkown:
                        ms = new MCvScalar(0, 0, 255);
                        break;
                    default:
                        break;
                }

                var vvp = new VectorOfVectorOfPoint();
                vvp.Push(vp);
                CvInvoke.DrawContours(test, vvp, -1, ms, -1);
                vvp.Dispose();
            }
            return test;

        }
    }
}


