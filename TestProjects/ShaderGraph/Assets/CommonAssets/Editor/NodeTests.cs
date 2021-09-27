using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.ShaderGraph.UnitTests
{
    class NodeTests
    {
        [UnityTest]
        public IEnumerator TransformV1MatchesOldTransform()
        {
            string kGraphName = "Assets/CommonAssets/Graphs/TransformGraph.shadergraph";

            List<PropertyCollector.TextureInfo> lti;
            var assetCollection = new AssetCollection();
            ShaderGraphImporter.GetShaderText(kGraphName, out lti, assetCollection, out var graph);
            Assert.NotNull(graph, $"Invalid graph data found for {kGraphName}");
            graph.OnEnable();
            graph.ValidateGraph();

            var renderer = new ShaderGraphTestRenderer();

            // first check that it renders red in the initial state, to check that the test works
            // (graph is initially set up with non-matching transforms)
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(128, 128, GraphicsFormat.R8G8B8A8_SRGB, depthBufferBits: 32);
                var target = RenderTexture.GetTemporary(descriptor);

                // use a non-standard transform, so that view, object, etc. transforms are non trivial
                renderer.RenderQuadPreview(graph, target, new Vector3(1.24699998f, 1.51900005f, 0.328999996f), new Quaternion(-0.164710045f, -0.0826543793f, -0.220811233f, 0.957748055f), useSRP: true);

                int incorrectPixels = ShaderGraphTestRenderer.CountPixelsNotEqual(target, new Color32(0, 255, 0, 255), false);
                Assert.AreEqual(128*128, incorrectPixels, "Number of incorrect pixels was not zero: initial state");

                if (incorrectPixels != 128 * 123)
                    ShaderGraphTestRenderer.SaveToPNG(target, "test-results/NodeTests/TransformNodeOld_default.png");

                RenderTexture.ReleaseTemporary(target);
                yield return null;
            }

            var xform = graph.GetNodes<TransformNode>().First();
            var old = graph.GetNodes<OldTransformNode>().First();

            // now check all possible settings
            var oldConversionTypes = new ConversionType[] { ConversionType.Position, ConversionType.Direction };
            foreach (ConversionType conversionType in oldConversionTypes)
            {
                foreach (CoordinateSpace source in Enum.GetValues(typeof(CoordinateSpace)))
                {
                    foreach (CoordinateSpace dest in Enum.GetValues(typeof(CoordinateSpace)))
                    {
                        // setup transform(v1) node
                        xform.conversion = new CoordinateSpaceConversion(source, dest);
                        xform.conversionType = conversionType;
                        xform.normalize = false;

                        // setup old transform node
                        old.conversion = new CoordinateSpaceConversion(source, dest);
                        old.conversionType = conversionType;

                        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(128, 128, GraphicsFormat.R8G8B8A8_SRGB, depthBufferBits: 32);
                        var target = RenderTexture.GetTemporary(descriptor);

                        // Debug.Log($"Tested: {source} to {dest} ({conversionType})");

                        // use a non-standard transform, so that view, object, etc. transforms are non trivial
                        renderer.RenderQuadPreview(graph, target, new Vector3(1.24699998f, 1.51900005f, 4.328999996f), new Quaternion(-0.164710045f, -0.0826543793f, -0.220811233f, 0.957748055f), useSRP: true);

                        int incorrectPixels = ShaderGraphTestRenderer.CountPixelsNotEqual(target, new Color32(0, 255, 0, 255), false);
                        Assert.AreEqual(0, incorrectPixels, $"Number of incorrect pixels was not zero: {source} to {dest} ({conversionType})");

                        if (incorrectPixels != 0)
                            ShaderGraphTestRenderer.SaveToPNG(target, $"test-results/NodeTests/TransformNodeOld_{source}_to_{dest}_{conversionType}.png");

                        RenderTexture.ReleaseTemporary(target);
                    }

                    // have to yield to let a frame pass
                    // unity only releases some resources at the end of a frame
                    // and if you do too many renders in a frame it will run out                        
                    yield return null;
                }
            }
        }
    }
}
