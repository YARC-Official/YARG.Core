﻿// Generated by Sichem at 12/24/2021 8:28:15 PM
/*
 * Public Domain STBImageSharp by Roman Shapiro
 */

using Hebron.Runtime;
using YARG.Core.IO;

namespace StbImageSharp
{
	unsafe partial class StbImage
	{
		public static bool stbi__hdr_test(stbi__context* s)
		{
			var r = stbi__hdr_test_core(s, "#?RADIANCE\n");
			stbi__rewind(s);
			if (r)
			{
				r = stbi__hdr_test_core(s, "#?RGBE\n");
				stbi__rewind(s);
			}

			return r;
		}

		public static bool stbi__hdr_load(stbi__context* s, int* x, int* y, int* comp, stbi__result_info* ri, out FixedArray<byte> result)
		{
            result = FixedArray<byte>.Null;
            var rgbe = stackalloc byte[4];
			var buffer = stackalloc sbyte[1024];
			sbyte* token;
			var valid = 0;
			var width = 0;
			var height = 0;
			var len = 0;
			byte count = 0;
			byte value = 0;
			var i = 0;
			var j = 0;
			var k = 0;
			var c1 = 0;
			var c2 = 0;
			var z = 0;
			sbyte* headerToken;
			headerToken = stbi__hdr_gettoken(s, buffer);
			if (CRuntime.strcmp(headerToken, "#?RADIANCE") != 0 && CRuntime.strcmp(headerToken, "#?RGBE") != 0)
				return false;
			for (; ; )
			{
				token = stbi__hdr_gettoken(s, buffer);
				if (token[0] == 0)
					break;
				if (CRuntime.strcmp(token, "FORMAT=32-bit_rle_rgbe") == 0)
					valid = 1;
			}

			if (valid == 0)
				return false;
			token = stbi__hdr_gettoken(s, buffer);
			if (CRuntime.strncmp(token, "-Y ", 3) != 0)
				return false;
			token += 3;
			height = (int)CRuntime.strtol(token, &token, 10);
			while (*token == 32)
				++token;
			if (CRuntime.strncmp(token, "+X ", 3) != 0)
				return false;
			token += 3;
			width = (int)CRuntime.strtol(token, null, 10);
			if (height > 1 << 24)
				return false;
			if (width > 1 << 24)
				return false;
			*x = width;
			*y = height;
			if (comp != null)
				*comp = 3;
			if (stbi__mad4sizes_valid(width, height, 3, sizeof(float), 0) == 0)
				return false;
			if (!stbi__malloc_mad4(width, height, 3, sizeof(float), 0, out result))
				return false;
            float* hdr_data = (float*) result.Ptr;
            FixedArray<byte> scanline;
			if (width < 8 || width >= 32768)
			{
				for (; j < height; ++j)
				{
					for (; i < width; ++i)
					{
						//var rgbe = stackalloc byte[4];
						stbi__getn(s, rgbe, 4);
						stbi__hdr_convert(hdr_data + j * width * 3 + i * 3, rgbe);
					}
				}
			}
			else
			{
                scanline = FixedArray<byte>.Null;
                for (j = 0; j < height; ++j)
				{
					c1 = stbi__get8(s);
					c2 = stbi__get8(s);
					len = stbi__get8(s);
					if (c1 != 2 || c2 != 2 || (len & 0x80) != 0)
					{
						//var rgbe = stackalloc byte[4];
						rgbe[0] = (byte)c1;
						rgbe[1] = (byte)c2;
						rgbe[2] = (byte)len;
						rgbe[3] = stbi__get8(s);
						stbi__hdr_convert(hdr_data, rgbe);
                        scanline.Dispose();

						// goto main_decode_loop

						// Do first row
						j = 0;
						for (i = 1; i < width; ++i)
						{
							//var rgbe = stackalloc byte[4];
							stbi__getn(s, rgbe, 4);
							stbi__hdr_convert(hdr_data + j * width * 3 + i * 3, rgbe);
						}

						// Do the rest of the rows.
						for (j = 1; j < height; ++j)
						{
							for (i = 0; i < width; ++i)
							{
								//var rgbe = stackalloc byte[4];
								stbi__getn(s, rgbe, 4);
                                stbi__hdr_convert(hdr_data + j * width * 3 + i * 3, rgbe);
                            }
						}

						goto finish;
					}

					len <<= 8;
					len |= stbi__get8(s);
					if (len != width)
					{
                        result.Dispose();
                        scanline.Dispose();
						return false;
					}

					if (!scanline.IsAllocated)
					{
						if (!stbi__malloc_mad2(width, 4, 0, out scanline))
						{
							result.Dispose();
							return false;
						}
					}

					for (k = 0; k < 4; ++k)
					{
						var nleft = 0;
						i = 0;
						while ((nleft = width - i) > 0)
						{
							count = stbi__get8(s);
							if (count > 128)
							{
								value = stbi__get8(s);
								count -= 128;
								if (count == 0 || count > nleft)
								{
									result.Dispose();
									scanline.Dispose();
									return false;
								}

								for (z = 0; z < count; ++z)
									scanline[i++ * 4 + k] = value;
							}
							else
							{
								if (count == 0 || count > nleft)
								{
									result.Dispose();
									scanline.Dispose();
									return false;
								}

								for (z = 0; z < count; ++z)
									scanline[i++ * 4 + k] = stbi__get8(s);
							}
						}
					}

					for (i = 0; i < width; ++i)
                        stbi__hdr_convert(hdr_data + (j * width + i) * 3, scanline.Ptr + i * 4);
                }
                scanline.Dispose();
			}

			finish:
			return true;
		}

		public static bool stbi__hdr_to_ldr(ref FixedArray<byte> orig, int x, int y, int comp, out FixedArray<byte> result)
		{
			var i = 0;
			var k = 0;
			var n = 0;
			if (!stbi__malloc_mad3(x, y, comp, 0, out result))
			{
				orig.Dispose();
				return false;
			}

			if ((comp & 1) != 0)
				n = comp;
			else
				n = comp - 1;
            float* data = (float*)orig.Ptr;
			for (i = 0; i < x * y; ++i)
			{
				for (k = 0; k < n; ++k)
				{
					var z = (float)CRuntime.pow(data[i * comp + k] * stbi__h2l_scale_i, stbi__h2l_gamma_i) * 255 +
							0.5f;
					if (z < 0)
						z = 0;
					if (z > 255)
						z = 255;
					result[i * comp + k] = (byte)(int)z;
				}

				if (k < comp)
				{
					var z = data[i * comp + k] * 255 + 0.5f;
					if (z < 0)
						z = 0;
					if (z > 255)
						z = 255;
                    result[i * comp + k] = (byte)(int)z;
				}
			}

			orig.Dispose();
			return true;
		}

		public static bool stbi__hdr_test_core(stbi__context* s, string signature)
		{
			var i = 0;
			for (i = 0; i < signature.Length; ++i)
				if (stbi__get8(s) != signature[i])
					return false;
			stbi__rewind(s);
			return true;
		}

		public static sbyte* stbi__hdr_gettoken(stbi__context* s, sbyte* buffer)
		{
			var len = 0;
			sbyte c = 0;
			c = (sbyte)stbi__get8(s);
			while (!stbi__at_eof(s) && c != 10)
			{
				buffer[len++] = c;
				if (len == 1024 - 1)
				{
					while (!stbi__at_eof(s) && stbi__get8(s) != 10)
					{
					}

					break;
				}

				c = (sbyte)stbi__get8(s);
			}

			buffer[len] = 0;
			return buffer;
		}

		public static void stbi__hdr_convert(float* output, byte* input)
		{
			if (input[3] != 0)
			{
				float f1 = 0;
				f1 = (float)CRuntime.ldexp(1.0f, input[3] - (128 + 8));
                output[0] = input[0] * f1;
                output[1] = input[1] * f1;
                output[2] = input[2] * f1;
			}
			else
			{
                output[0] = output[1] = output[2] = 0;
            }
		}
	}
}