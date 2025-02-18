﻿using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using Emgu.CV.CvEnum;

namespace ShowOpenCVResult.Windows
{
    public partial class FinalTest : MoveBlock
    {
        public FinalTest()
        {
            InitializeComponent();
        }


        private void imageIO1_DoImgChange(object sender, EventArgs e)
        {
            if (imageIO1.InImage == null) return;

            var img = (imageIO1.InImage as Image<Bgr, Byte>).Mat;
            long time = 0;
            Mat  result = null;
            LineSegment2D[] lines = null;
            if (toolStripButton2.Checked)
                result = OpencvMath.SpeedProcess(img, out time, out lines, true);
            else
                result = OpencvMath.SpeedProcessNoWarp(img, out time);

            if (imageIO1.OutImage != null)
            {
                imageIO1.OutImage.Dispose();
            }

            imageIO1.OutImage = result;

            MessageBox.Show(string.Format("耗时{0}毫秒", time));
        }



        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            var img = OpencvForm.GetImage();
            if (img == null) return;

            imageIO1.SetInput(img);
        }

        private void myTrackBar1_ValueChanged(object sender, EventArgs e)
        {
            imageIO1.DoChange();
        }

        private void imageIO1_AfterImgLoaded(object sender, EventArgs e)
        {
           
        }

        private void toolStripButton2_CheckedChanged(object sender, EventArgs e)
        {
            imageIO1.DoChange();
        }
    }
}
