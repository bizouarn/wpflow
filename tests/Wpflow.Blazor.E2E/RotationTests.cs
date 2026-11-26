using System.Globalization;
using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

/// <summary>Un node tourné à la poignée emporte ses edges : ils aboutissent au handle tel qu'il est affiché.</summary>
public abstract class RotationTests(DemoFixture demo) : PagesTest(demo)
{
    private const string Handle = "[data-node-id='r'] .wf-handle[data-type='target']";

    /// <summary>La plus petite distance, à l'écran, entre un bout de tracé d'edge et le centre du handle.</summary>
    private static async Task<(double Distance, (double X, double Y) Handle)> GapAsync(IPage page)
    {
        var handle = await page.CenterAsync(Handle);
        var gap = await page.EvaluateAsync<double>("""
            ([x, y]) => Math.min(...[...document.querySelectorAll('.wf-edge-path')].flatMap(path => {
                const m = path.getScreenCTM();
                return [0, path.getTotalLength()].map(at => {
                    const p = path.getPointAtLength(at).matrixTransform(m);
                    return Math.hypot(p.x - x, p.y - y);
                });
            }))
            """, new[] { handle.X, handle.Y });
        return (gap, handle);
    }

    [Fact]
    public async Task The_edges_stay_on_the_handles_of_a_rotated_node()
    {
        var page = await App.OpenAsync();
        var zoom = (await page.ViewportAsync()).Zoom;
        var before = await GapAsync(page);
        Assert.InRange(before.Distance, 0, 6 * zoom);

        var node = await page.CenterAsync("[data-node-id='r']");
        await page.Mouse.ClickAsync((float)node.X, (float)node.Y);
        await page.Locator("[data-node-id='r'] .wf-rotator").WaitForAsync();

        // Quelques angles quelconques, puis un quart de tour : la poignée est tirée autour du centre du node.
        foreach (var degrees in new[] { 35.0, 140.0, -70.0, 90.0 })
        {
            var grip = await page.CenterAsync("[data-node-id='r'] .wf-rotator");
            var radians = degrees * Math.PI / 180;
            await page.DragAsync(grip, (node.X + 120 * Math.Sin(radians), node.Y - 120 * Math.Cos(radians)));

            var (distance, handle) = (double.MaxValue, (X: 0.0, Y: 0.0));
            var until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until)
            {
                (distance, handle) = await GapAsync(page);
                if (distance <= 6 * zoom) break;
                await page.WaitForTimeoutAsync(100);
            }

            Assert.True(distance <= 6 * zoom, string.Create(CultureInfo.InvariantCulture,
                $"à {degrees}° : l'edge est à {distance:0.0} px du handle ({handle.X:0}, {handle.Y:0})"));
            // Le handle s'est bien éloigné de sa place d'origine : un edge resté là échouerait.
            Assert.True(Math.Abs(handle.X - before.Handle.X) + Math.Abs(handle.Y - before.Handle.Y) > 10 * zoom);
        }
    }
}

public sealed class ServerRotationTests(ServerFixture demo) : RotationTests(demo), IClassFixture<ServerFixture>;

public sealed class WasmRotationTests(WasmFixture demo) : RotationTests(demo), IClassFixture<WasmFixture>;
