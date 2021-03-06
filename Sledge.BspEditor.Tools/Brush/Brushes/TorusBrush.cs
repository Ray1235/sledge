﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Tools.Brush.Brushes.Controls;
using Sledge.Common;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Hooks;
using Sledge.Common.Translations;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Tools.Brush.Brushes
{
    [Export(typeof(IBrush))]
    [Export(typeof(IInitialiseHook))]
    [OrderHint("J")]
    [AutoTranslate]
    public class TorusBrush : IBrush, IInitialiseHook
    {
        private NumericControl _crossSides;
        private NumericControl _crossRadius;
        private BooleanControl _crossMakeHollow;
        private NumericControl _crossArc;
        private NumericControl _crossStartAngle;
        private NumericControl _crossWallWidth;

        private NumericControl _ringSides;
        private NumericControl _ringArc;
        private NumericControl _ringStartAngle;

        private NumericControl _rotationHeight;

        public string CrossSectionSides { get; set; }
        public string RingWidth { get; set; }
        public string CrossSectionStart { get; set; }
        public string MakeHollow { get; set; }
        public string CrossSectionArc { get; set; }
        public string HollowWallWidth { get; set; }
        public string RingSides { get; set; }
        public string RingArc { get; set; }
        public string RingStart { get; set; }
        public string RotationHeight { get; set; }

        public async Task OnInitialise()
        {
            _crossSides = new NumericControl(this) { LabelText = CrossSectionSides };
            _crossRadius = new NumericControl(this) { LabelText = RingWidth, Minimum = 16, Maximum = 1024, Value = 32, Precision = 1 };
            _crossStartAngle = new NumericControl(this) { LabelText = CrossSectionStart, Minimum = 0, Maximum = 359, Value = 0 };
            _crossMakeHollow = new BooleanControl(this) { LabelText = MakeHollow, Checked = false };
            _crossArc = new NumericControl(this) { LabelText = CrossSectionArc, Minimum = 1, Maximum = 360, Value = 360, Enabled = false };
            _crossWallWidth = new NumericControl(this) { LabelText = HollowWallWidth, Minimum = 1, Maximum = 1024, Value = 16, Precision = 1, Enabled = false};
            _ringSides = new NumericControl(this) { LabelText = RingSides };
            _ringArc = new NumericControl(this) { LabelText = RingArc, Minimum = 1, Maximum = 1080, Value = 360 };
            _ringStartAngle = new NumericControl(this) { LabelText = RingStart, Minimum = 0, Maximum = 359, Value = 0 };
            _rotationHeight = new NumericControl(this) { LabelText = RotationHeight, Minimum = -1024, Maximum = 1024, Value = 0, Precision = 1};
            _crossMakeHollow.ValuesChanged += (s, b) => _crossWallWidth.Enabled = _crossArc.Enabled = _crossMakeHollow.GetValue();
        }

        public string Name { get; set; } = "Torus";

        public bool CanRound => true;

        public IEnumerable<BrushControl> GetControls()
        {
            yield return _crossSides;
            yield return _crossRadius;
            yield return _crossStartAngle;
            yield return _crossMakeHollow;
            yield return _crossArc;
            yield return _crossWallWidth;
            yield return _ringSides;
            yield return _ringArc;
            yield return _ringStartAngle;
            yield return _rotationHeight;
        }

        private Solid MakeSolid(UniqueNumberGenerator generator, IEnumerable<Coordinate[]> faces, string texture, Color col)
        {
            var solid = new Solid(generator.Next("MapObject"));
            solid.Data.Add(new ObjectColor(Colour.GetRandomBrushColour()));

            foreach (var arr in faces)
            {
                var face = new Face(generator.Next("Face"))
                {
                    Plane = new Plane(arr[0], arr[1], arr[2]),
                    Texture = { Name = texture}
                };
                face.Vertices.AddRange(arr);
                solid.Data.Add(face);
            }
            solid.DescendantsChanged();
            return solid;
        }

        public IEnumerable<IMapObject> Create(UniqueNumberGenerator generator, Box box, string texture, int roundDecimals)
        {
            var crossSides = (int)_crossSides.GetValue();
            if (crossSides < 3) yield break;
            var crossWidth = _crossRadius.GetValue() * 2;
            if (crossWidth < 1) yield break;
            var crossMakeHollow = _crossMakeHollow.GetValue();
            var crossArc = !crossMakeHollow ? 360 : (int)_crossArc.GetValue();
            if (crossArc < 1) yield break;
            var crossStartAngle = (int)_crossStartAngle.GetValue();
            if (crossStartAngle < 0 || crossStartAngle > 359) yield break;
            var crossWallWidth = _crossWallWidth.GetValue();
            if (crossWallWidth < 1) yield break;
            var ringSides = (int)_ringSides.GetValue();
            if (ringSides < 3) yield break;
            var ringArc = (int)_ringArc.GetValue();
            if (ringArc < 1) yield break;
            var ringStartAngle = (int)_ringStartAngle.GetValue();
            if (ringStartAngle < 0 || ringStartAngle > 359) yield break;
            var rotationHeight = _rotationHeight.GetValue();

            // Sort of a combination of cylinder and pipe brushes
            var width = box.Width;
            var length = box.Length;
            var height = box.Height;
            var majorPrimary = (width - crossWidth) / 2; // Primary = donut circle
            var minorPrimary = (length - crossWidth) / 2;
            var majorSecondaryOuter = crossWidth / 2; // Secondary = cross section circle
            var minorSecondaryOuter = height / 2; // Outer = Outer ring
            var majorSecondaryInner = (crossWidth - crossWallWidth) / 2; // Inner = inner ring (hollow only)
            var minorSecondaryInner = (height - crossWallWidth) / 2;

            var ringStart = DMath.DegreesToRadians(ringStartAngle);
            var ringAngle = DMath.DegreesToRadians(ringArc) / ringSides;
            var crossStart = DMath.DegreesToRadians(crossStartAngle);
            var crossAngle = DMath.DegreesToRadians(crossArc) / crossSides;
            var heightAdd = rotationHeight / ringSides;

            // Rotate around the ring, generating each cross section
            var ringOuterSections = new List<Coordinate[]>();
            var ringInnerSections = new List<Coordinate[]>();
            for (var i = 0; i < ringSides + 1; i++)
            {
                var ring = ringStart + i * ringAngle;
                var rxval = box.Center.X + majorPrimary * DMath.Cos(ring);
                var ryval = box.Center.Y + minorPrimary * DMath.Sin(ring);
                var rzval = box.Center.Z;
                var crossSecOuter = new Coordinate[crossSides + 1];
                var crossSecInner = new Coordinate[crossSides + 1];
                for (var j = 0; j < crossSides + 1; j++)
                {
                    var cross = crossStart + j * crossAngle;
                    var xval = majorSecondaryOuter * DMath.Cos(cross) * DMath.Cos(ring);
                    var yval = majorSecondaryOuter * DMath.Cos(cross) * DMath.Sin(ring);
                    var zval = minorSecondaryOuter * DMath.Sin(cross);
                    crossSecOuter[j] = new Coordinate(xval + rxval, yval + ryval, zval + rzval).Round(roundDecimals);
                    if (!crossMakeHollow) continue;
                    xval = majorSecondaryInner * DMath.Cos(cross) * DMath.Cos(ring);
                    yval = majorSecondaryInner * DMath.Cos(cross) * DMath.Sin(ring);
                    zval = minorSecondaryInner * DMath.Sin(cross);
                    crossSecInner[j] = new Coordinate(xval + rxval, yval + ryval, zval + rzval).Round(roundDecimals);
                }
                ringOuterSections.Add(crossSecOuter);
                ringInnerSections.Add(crossSecInner);
            }

            // Create the solids
            var colour = Colour.GetRandomBrushColour();
            for (var i = 0; i < ringSides; i++)
            {
                var vertical = Coordinate.UnitZ * heightAdd * i;
                var nexti = i + 1;
                if (crossMakeHollow)
                {
                    // Use pipe cross sections
                    var outerPoints = ringOuterSections[i];
                    var nextOuterPoints = ringOuterSections[nexti];
                    var innerPoints = ringInnerSections[i];
                    var nextInnerPoints = ringInnerSections[nexti];
                    for (var j = 0; j < crossSides; j++)
                    {
                        var nextj = j + 1;
                        var faces = new List<Coordinate[]>();
                        faces.Add(new[] { outerPoints[j], outerPoints[nextj], nextOuterPoints[nextj], nextOuterPoints[j] }.Select(x => x + vertical).ToArray());
                        faces.Add(new[] { nextInnerPoints[j], nextInnerPoints[nextj], innerPoints[nextj], innerPoints[j] }.Select(x => x + vertical).ToArray());
                        faces.Add(new[] { innerPoints[nextj], nextInnerPoints[nextj], nextOuterPoints[nextj], outerPoints[nextj] }.Select(x => x + vertical).ToArray());
                        faces.Add(new[] { outerPoints[j], nextOuterPoints[j], nextInnerPoints[j], innerPoints[j] }.Select(x => x + vertical).ToArray());
                        faces.Add(new[] { innerPoints[j], innerPoints[nextj], outerPoints[nextj], outerPoints[j] }.Select(x => x + vertical).ToArray());
                        faces.Add(new[] { nextOuterPoints[j], nextOuterPoints[nextj], nextInnerPoints[nextj], nextInnerPoints[j] }.Select(x => x + vertical).ToArray());
                        yield return MakeSolid(generator, faces, texture, colour);
                    }
                }
                else
                {
                    // Use cylindrical cross sections
                    var faces = new List<Coordinate[]>();
                    var points = ringOuterSections[i];
                    var nextPoints = ringOuterSections[nexti];
                    // Add the outer faces
                    for (var j = 0; j < crossSides; j++)
                    {
                        var nextj = (j + 1) % crossSides;
                        faces.Add(new[] { points[j], points[nextj], nextPoints[nextj], nextPoints[j] }.Select(x => x + vertical).ToArray());
                    }
                    // Add the cross section faces
                    faces.Add(points.Reverse().Select(x => x + vertical).ToArray());
                    faces.Add(nextPoints.Select(x => x + vertical).ToArray());
                    yield return MakeSolid(generator, faces, texture, colour);
                }
            }
        }
    }
}
