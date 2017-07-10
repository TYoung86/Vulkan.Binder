using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace VulkanTests {
	public class VkClearValueFamilyTests {

		/// <summary>
		/// This tests nestings of array-like structures contained in unions.
		/// </summary>
		[Fact]
		public void VkClearValueTypeLoad() {
			// float values as ints
			const uint oneF = 0x3f800000u;
			const uint twoF = 0x40000000u;
			const uint threeF = 0x40400000u;
			const uint fourF = 0x40800000u;

			// structural composition
			var ca = new VkClearAttachment {
				aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
				clearValue = new VkClearValue {
					color = new VkClearColorValue()
				},
				colorAttachment = 0
			};

			ref var color = ref ca.clearValue.color;

			// assignment
			color.float32(0) = 1;
			color.float32(1) = 2;
			color.float32(2) = 3;
			color.float32(3) = 4;

			// validation of aliasing
			Assert.StrictEqual(oneF, color.uint32(0));
			Assert.StrictEqual(twoF, color.uint32(1));
			Assert.StrictEqual(threeF, color.uint32(2));
			Assert.StrictEqual(fourF, color.uint32(3));
			
			Assert.StrictEqual((int) oneF, color.int32(0));
			Assert.StrictEqual((int) twoF, color.int32(1));
			Assert.StrictEqual((int) threeF, color.int32(2));
			Assert.StrictEqual((int) fourF, color.int32(3));

			Assert.StrictEqual(1, color.float32(0));
			Assert.StrictEqual(2, color.float32(1));
			Assert.StrictEqual(3, color.float32(2));
			Assert.StrictEqual(4, color.float32(3));

			ref var depthStencil = ref ca.clearValue.depthStencil;

			Assert.StrictEqual(1, depthStencil.depth);
			Assert.StrictEqual(twoF, depthStencil.stencil);
		}

	}
}