namespace Tower.Core;

public readonly record struct ProjectedPoint(double X, double Y);

public readonly record struct TowerProjection(Position Observer, double Bearing, double FieldOfView, int Width, int Height, double EyeHeight = 40, double Pitch = 0)
{
    private double Focal => Width / (2 * Math.Tan(FieldOfView * Math.PI / 360));

    public double Depth(Position point, double altitudeMetres = 0) => Camera(point, altitudeMetres).Depth;

    private (double X, double Depth, double Up) Camera(Position point, double altitude = 0)
    {
        var angle = Bearing * Math.PI / 180;
        var dx = point.X - Observer.X;
        var dy = point.Y - Observer.Y;
        var forward = dx * Math.Sin(angle) + dy * Math.Cos(angle);
        var up = altitude - EyeHeight;
        var pitch = Pitch * Math.PI / 180;
        return (dx * Math.Cos(angle) - dy * Math.Sin(angle),
            forward * Math.Cos(pitch) + up * Math.Sin(pitch), up * Math.Cos(pitch) - forward * Math.Sin(pitch));
    }

    public ProjectedPoint? Point(Position point, double altitudeMetres = 0)
    {
        if (Width <= 0 || Height <= 0)
        {
            return null;
        }

        var camera = Camera(point, altitudeMetres);
        if (camera.Depth < 1)
        {
            return null;
        }

        var x = Width / 2.0 + camera.X * Focal / camera.Depth;
        var y = Height * 0.43 - camera.Up * Focal / camera.Depth;
        return x >= 0 && x <= Width && y >= 0 && y <= Height ? new(x, y) : null;
    }

    public ProjectedPoint[] Polygon(IReadOnlyList<(Position Position, double Altitude)> points)
    {
        if (Width <= 0 || Height <= 0 || points.Count < 3)
        {
            return [];
        }
        var polygon = new List<(double X, double Depth, double Up)>();
        foreach (var point in points)
        {
            var camera = Camera(point.Position, point.Altitude);
            polygon.Add(camera);
        }
        var halfView = Math.Tan(FieldOfView * Math.PI / 360);
        var width = Width;
        var height = Height;
        var focal = Focal;
        void Clip(Func<(double X, double Depth, double Up), double> distance)
        {
            if (polygon.Count == 0)
            {
                return;
            }
            var output = new List<(double X, double Depth, double Up)>();
            var previous = polygon[^1];
            var previousDistance = distance(previous);
            foreach (var current in polygon)
            {
                var currentDistance = distance(current);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                {
                    var fraction = previousDistance / (previousDistance - currentDistance);
                    output.Add((previous.X + (current.X - previous.X) * fraction,
                        previous.Depth + (current.Depth - previous.Depth) * fraction,
                        previous.Up + (current.Up - previous.Up) * fraction));
                }
                if (currentDistance >= 0)
                {
                    output.Add(current);
                }
                previous = current;
                previousDistance = currentDistance;
            }
            polygon = output;
        }
        Clip(point => point.Depth - 1);
        Clip(point => point.Depth * halfView - point.X);
        Clip(point => point.Depth * halfView + point.X);
        Clip(point => point.Depth * height * 0.43 / focal - point.Up);
        Clip(point => point.Depth * height * 0.57 / focal + point.Up);
        return polygon.Select(point => new ProjectedPoint(
            Math.Clamp(width / 2d + point.X * focal / point.Depth, 0, width),
            Math.Clamp(height * 0.43 - point.Up * focal / point.Depth, 0, height))).ToArray();
    }

    public ProjectedPoint[] GroundPolygon(IReadOnlyList<Position> points)
    {
        if (Width <= 0 || Height <= 0 || points.Count < 3)
        {
            return [];
        }

        if (Pitch != 0)
        {
            return Polygon(points.Select(point => (point, 0d)).ToArray());
        }
        var projection = this;
        var polygon = points.Select(point => { var camera = projection.Camera(point); return (camera.X, camera.Depth); }).ToList();
        var halfView = Math.Tan(FieldOfView * Math.PI / 360);
        var near = Pitch == 0 ? Math.Max(1, EyeHeight * Focal / (Height * 0.57)) : 1;

        void Clip(Func<(double X, double Depth), double> distance)
        {
            if (polygon.Count == 0)
            {
                return;
            }

            var output = new List<(double X, double Depth)>();
            var previous = polygon[^1];
            var previousDistance = distance(previous);
            foreach (var current in polygon)
            {
                var currentDistance = distance(current);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                {
                    var fraction = previousDistance / (previousDistance - currentDistance);
                    output.Add((previous.X + (current.X - previous.X) * fraction,
                        previous.Depth + (current.Depth - previous.Depth) * fraction));
                }
                if (currentDistance >= 0)
                {
                    output.Add(current);
                }

                previous = current;
                previousDistance = currentDistance;
            }

            polygon = output;
        }

        Clip(point => point.Depth - near);
        Clip(point => point.Depth * halfView - point.X);
        Clip(point => point.Depth * halfView + point.X);
        var width = Width;
        var height = Height;
        var focal = Focal;
        var eyeHeight = EyeHeight;
        return polygon.Select(point => new ProjectedPoint(
            Math.Clamp(width / 2.0 + point.X * focal / point.Depth, 0, width),
            Math.Clamp(height * 0.43 + eyeHeight * focal / point.Depth, 0, height))).ToArray();
    }

    public (ProjectedPoint From, ProjectedPoint To)? GroundSegment(Position from, Position to)
    {
        if (Width <= 0 || Height <= 0)
        {
            return null;
        }

        var a = Camera(from);
        var b = Camera(to);
        var focal = Focal;
        var width = Width;
        var height = Height;
        var start = 0.0;
        var finish = 1.0;
        var halfView = Math.Tan(FieldOfView * Math.PI / 360);
        var near = Pitch == 0 ? Math.Max(1, EyeHeight * Focal / (Height * 0.57)) : 1;

        bool Clip(double first, double last)
        {
            if (first < 0 && last < 0)
            {
                return false;
            }

            if (first < 0)
            {
                start = Math.Max(start, first / (first - last));
            }
            else if (last < 0)
            {
                finish = Math.Min(finish, first / (first - last));
            }

            return start <= finish;
        }

        if (!Clip(a.Depth - near, b.Depth - near) ||
            !Clip(a.Depth * halfView - a.X, b.Depth * halfView - b.X) ||
            !Clip(a.Depth * halfView + a.X, b.Depth * halfView + b.X) ||
            !Clip(a.Depth * height * 0.43 / focal - a.Up, b.Depth * height * 0.43 / focal - b.Up) ||
            !Clip(a.Depth * height * 0.57 / focal + a.Up, b.Depth * height * 0.57 / focal + b.Up))
        {
            return null;
        }

        ProjectedPoint At(double fraction)
        {
            var x = a.X + (b.X - a.X) * fraction;
            var depth = a.Depth + (b.Depth - a.Depth) * fraction;
            var up = a.Up + (b.Up - a.Up) * fraction;
            return new(Math.Clamp(width / 2.0 + x * focal / depth, 0, width),
                Math.Clamp(height * 0.43 - up * focal / depth, 0, height));
        }

        return (At(start), At(finish));
    }
}
