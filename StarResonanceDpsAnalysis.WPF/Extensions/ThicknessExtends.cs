using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StarResonanceDpsAnalysis.Core.Extends.System.Windows
{
    public static class ThicknessExtends
    {
        public static Thickness Add(this Thickness th1, Thickness th2)
        {
            return new Thickness
            {
                Left = th1.Left + th2.Left,
                Top = th1.Top + th2.Top,
                Right = th1.Right + th2.Right,
                Bottom = th1.Bottom + th2.Bottom,
            };
        }
    }
}
