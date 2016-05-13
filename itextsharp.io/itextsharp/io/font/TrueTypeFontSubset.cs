/*
$Id: 88b487e2fab02001985779cf3c1048d8e2492e32 $

This file is part of the iText (R) project.
Copyright (c) 1998-2016 iText Group NV
Authors: Bruno Lowagie, Paulo Soares, et al.

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License version 3
as published by the Free Software Foundation with the addition of the
following permission added to Section 15 as permitted in Section 7(a):
FOR ANY PART OF THE COVERED WORK IN WHICH THE COPYRIGHT IS OWNED BY
ITEXT GROUP. ITEXT GROUP DISCLAIMS THE WARRANTY OF NON INFRINGEMENT
OF THIRD PARTY RIGHTS

This program is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
See the GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program; if not, see http://www.gnu.org/licenses or write to
the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
Boston, MA, 02110-1301 USA, or download the license from the following URL:
http://itextpdf.com/terms-of-use/

The interactive user interfaces in modified source and object code versions
of this program must display Appropriate Legal Notices, as required under
Section 5 of the GNU Affero General Public License.

In accordance with Section 7(b) of the GNU Affero General Public License,
a covered work must retain the producer line in every PDF that is created
or manipulated using iText.

You can be released from the requirements of the license by purchasing
a commercial license. Buying such a license is mandatory as soon as you
develop commercial activities involving the iText software without
disclosing the source code of your own applications.
These activities include: offering paid services to customers as an ASP,
serving PDFs on the fly in a web application, shipping iText with a closed
source product.

For more information, please contact iText Software Corp. at this
address: sales@itextpdf.com
*/
using System;
using System.Collections.Generic;
using iTextSharp.IO;
using iTextSharp.IO.Source;
using iTextSharp.IO.Util;

namespace iTextSharp.IO.Font
{
	/// <summary>Subsets a True Type font by removing the unneeded glyphs from the font.</summary>
	/// <author>Paulo Soares</author>
	internal class TrueTypeFontSubset
	{
		internal static readonly String[] tableNamesSimple = new String[] { "cvt ", "fpgm"
			, "glyf", "head", "hhea", "hmtx", "loca", "maxp", "prep" };

		internal static readonly String[] tableNamesCmap = new String[] { "cmap", "OS/2" };

		internal static readonly String[] tableNamesExtra = new String[] { "cmap", "OS/2"
			, "name" };

		internal static readonly int[] entrySelectors = new int[] { 0, 0, 1, 1, 2, 2, 2, 
			2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4 };

		internal const int TABLE_CHECKSUM = 0;

		internal const int TABLE_OFFSET = 1;

		internal const int TABLE_LENGTH = 2;

		internal const int HEAD_LOCA_FORMAT_OFFSET = 51;

		internal const int ARG_1_AND_2_ARE_WORDS = 1;

		internal const int WE_HAVE_A_SCALE = 8;

		internal const int MORE_COMPONENTS = 32;

		internal const int WE_HAVE_AN_X_AND_Y_SCALE = 64;

		internal const int WE_HAVE_A_TWO_BY_TWO = 128;

		/// <summary>Contains the location of the several tables.</summary>
		/// <remarks>
		/// Contains the location of the several tables. The key is the name of
		/// the table and the value is an
		/// <c>int[3]</c>
		/// where position 0
		/// is the checksum, position 1 is the offset from the start of the file
		/// and position 2 is the length of the table.
		/// </remarks>
		protected internal IDictionary<String, int[]> tableDirectory;

		/// <summary>The file in use.</summary>
		protected internal RandomAccessFileOrArray rf;

		/// <summary>The file name.</summary>
		protected internal String fileName;

		protected internal bool includeCmap;

		protected internal bool includeExtras;

		protected internal bool locaShortTable;

		protected internal int[] locaTable;

		protected internal ICollection<int> glyphsUsed;

		protected internal IList<int> glyphsInList;

		protected internal int tableGlyphOffset;

		protected internal int[] newLocaTable;

		protected internal byte[] newLocaTableOut;

		protected internal byte[] newGlyfTable;

		protected internal int glyfTableRealSize;

		protected internal int locaTableRealSize;

		protected internal byte[] outFont;

		protected internal int fontPtr;

		protected internal int directoryOffset;

		/// <summary>Creates a new TrueTypeFontSubSet</summary>
		/// <param name="directoryOffset">The offset from the start of the file to the table directory
		/// 	</param>
		/// <param name="fileName">the file name of the font</param>
		/// <param name="glyphsUsed">the glyphs used</param>
		/// <param name="includeCmap">
		/// 
		/// <see langword="true"/>
		/// if the table cmap is to be included in the generated font
		/// </param>
		internal TrueTypeFontSubset(String fileName, RandomAccessFileOrArray rf, ICollection
			<int> glyphsUsed, int directoryOffset, bool includeCmap, bool includeExtras)
		{
			this.fileName = fileName;
			this.rf = rf;
			this.glyphsUsed = glyphsUsed;
			this.includeCmap = includeCmap;
			this.includeExtras = includeExtras;
			this.directoryOffset = directoryOffset;
			glyphsInList = new List<int>(glyphsUsed);
		}

		/// <summary>Does the actual work of subsetting the font.</summary>
		/// <exception cref="System.IO.IOException">on error</exception>
		/// <on>error</on>
		/// <returns>the subset font</returns>
		internal virtual byte[] Process()
		{
			try
			{
				CreateTableDirectory();
				ReadLoca();
				FlatGlyphs();
				CreateNewGlyphTables();
				LocaToBytes();
				AssembleFont();
				return outFont;
			}
			finally
			{
				try
				{
					rf.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void AssembleFont()
		{
			int[] tableLocation;
			int fullFontSize = 0;
			IList<String> tableNames = new List<String>();
			tableNames.AddAll(tableNamesSimple);
			if (includeExtras)
			{
				tableNames.AddAll(tableNamesExtra);
			}
			else
			{
				if (includeCmap)
				{
					tableNames.AddAll(tableNamesCmap);
				}
			}
			int tablesUsed = 2;
			foreach (String name in tableNames)
			{
				if (name.Equals("glyf") || name.Equals("loca"))
				{
					continue;
				}
				tableLocation = tableDirectory[name];
				if (tableLocation == null)
				{
					continue;
				}
				tablesUsed++;
				fullFontSize += tableLocation[TABLE_LENGTH] + 3 & ~3;
			}
			fullFontSize += newLocaTableOut.Length;
			fullFontSize += newGlyfTable.Length;
			int reference = 16 * tablesUsed + 12;
			fullFontSize += reference;
			outFont = new byte[fullFontSize];
			fontPtr = 0;
			WriteFontInt(0x00010000);
			WriteFontShort(tablesUsed);
			int selector = entrySelectors[tablesUsed];
			WriteFontShort((1 << selector) * 16);
			WriteFontShort(selector);
			WriteFontShort((tablesUsed - (1 << selector)) * 16);
			foreach (String name_1 in tableNames)
			{
				int len;
				tableLocation = tableDirectory[name_1];
				if (tableLocation == null)
				{
					continue;
				}
				WriteFontString(name_1);
				switch (name_1)
				{
					case "glyf":
					{
						WriteFontInt(CalculateChecksum(newGlyfTable));
						len = glyfTableRealSize;
						break;
					}

					case "loca":
					{
						WriteFontInt(CalculateChecksum(newLocaTableOut));
						len = locaTableRealSize;
						break;
					}

					default:
					{
						WriteFontInt(tableLocation[TABLE_CHECKSUM]);
						len = tableLocation[TABLE_LENGTH];
						break;
					}
				}
				WriteFontInt(reference);
				WriteFontInt(len);
				reference += len + 3 & ~3;
			}
			foreach (String name_2 in tableNames)
			{
				tableLocation = tableDirectory[name_2];
				if (tableLocation == null)
				{
					continue;
				}
				switch (name_2)
				{
					case "glyf":
					{
						System.Array.Copy(newGlyfTable, 0, outFont, fontPtr, newGlyfTable.Length);
						fontPtr += newGlyfTable.Length;
						newGlyfTable = null;
						break;
					}

					case "loca":
					{
						System.Array.Copy(newLocaTableOut, 0, outFont, fontPtr, newLocaTableOut.Length);
						fontPtr += newLocaTableOut.Length;
						newLocaTableOut = null;
						break;
					}

					default:
					{
						rf.Seek(tableLocation[TABLE_OFFSET]);
						rf.ReadFully(outFont, fontPtr, tableLocation[TABLE_LENGTH]);
						fontPtr += tableLocation[TABLE_LENGTH] + 3 & ~3;
						break;
					}
				}
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void CreateTableDirectory()
		{
			tableDirectory = new Dictionary<String, int[]>();
			rf.Seek(directoryOffset);
			int id = rf.ReadInt();
			if (id != 0x00010000)
			{
				throw new IOException("1.is.not.a.true.type.file").SetMessageParams(fileName);
			}
			int num_tables = rf.ReadUnsignedShort();
			rf.SkipBytes(6);
			for (int k = 0; k < num_tables; ++k)
			{
				String tag = ReadStandardString(4);
				int[] tableLocation = new int[3];
				tableLocation[TABLE_CHECKSUM] = rf.ReadInt();
				tableLocation[TABLE_OFFSET] = rf.ReadInt();
				tableLocation[TABLE_LENGTH] = rf.ReadInt();
				tableDirectory[tag] = tableLocation;
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void ReadLoca()
		{
			int[] tableLocation = tableDirectory["head"];
			if (tableLocation == null)
			{
				throw new IOException("table.1.does.not.exist.in.2", "head").SetMessageParams(fileName
					);
			}
			rf.Seek(tableLocation[TABLE_OFFSET] + HEAD_LOCA_FORMAT_OFFSET);
			locaShortTable = rf.ReadUnsignedShort() == 0;
			tableLocation = tableDirectory["loca"];
			if (tableLocation == null)
			{
				throw new IOException("table.1.does.not.exist.in.2", "loca").SetMessageParams(fileName
					);
			}
			rf.Seek(tableLocation[TABLE_OFFSET]);
			if (locaShortTable)
			{
				int entries = tableLocation[TABLE_LENGTH] / 2;
				locaTable = new int[entries];
				for (int k = 0; k < entries; ++k)
				{
					locaTable[k] = rf.ReadUnsignedShort() * 2;
				}
			}
			else
			{
				int entries = tableLocation[TABLE_LENGTH] / 4;
				locaTable = new int[entries];
				for (int k = 0; k < entries; ++k)
				{
					locaTable[k] = rf.ReadInt();
				}
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void CreateNewGlyphTables()
		{
			newLocaTable = new int[locaTable.Length];
			int[] activeGlyphs = new int[glyphsInList.Count];
			for (int k = 0; k < activeGlyphs.Length; ++k)
			{
				activeGlyphs[k] = glyphsInList[k];
			}
			System.Array.Sort(activeGlyphs);
			int glyfSize = 0;
			foreach (int glyph in activeGlyphs)
			{
				glyfSize += locaTable[glyph + 1] - locaTable[glyph];
			}
			glyfTableRealSize = glyfSize;
			glyfSize = glyfSize + 3 & ~3;
			newGlyfTable = new byte[glyfSize];
			int glyfPtr = 0;
			int listGlyf = 0;
			for (int k_1 = 0; k_1 < newLocaTable.Length; ++k_1)
			{
				newLocaTable[k_1] = glyfPtr;
				if (listGlyf < activeGlyphs.Length && activeGlyphs[listGlyf] == k_1)
				{
					++listGlyf;
					newLocaTable[k_1] = glyfPtr;
					int start = locaTable[k_1];
					int len = locaTable[k_1 + 1] - start;
					if (len > 0)
					{
						rf.Seek(tableGlyphOffset + start);
						rf.ReadFully(newGlyfTable, glyfPtr, len);
						glyfPtr += len;
					}
				}
			}
		}

		protected internal virtual void LocaToBytes()
		{
			if (locaShortTable)
			{
				locaTableRealSize = newLocaTable.Length * 2;
			}
			else
			{
				locaTableRealSize = newLocaTable.Length * 4;
			}
			newLocaTableOut = new byte[locaTableRealSize + 3 & ~3];
			outFont = newLocaTableOut;
			fontPtr = 0;
			for (int k = 0; k < newLocaTable.Length; ++k)
			{
				if (locaShortTable)
				{
					WriteFontShort(newLocaTable[k] / 2);
				}
				else
				{
					WriteFontInt(newLocaTable[k]);
				}
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void FlatGlyphs()
		{
			int[] tableLocation = tableDirectory["glyf"];
			if (tableLocation == null)
			{
				throw new IOException("table.1.does.not.exist.in.2").SetMessageParams("glyf", fileName
					);
			}
			int glyph0 = 0;
			if (!glyphsUsed.Contains(glyph0))
			{
				glyphsUsed.Add(glyph0);
				glyphsInList.Add(glyph0);
			}
			tableGlyphOffset = tableLocation[TABLE_OFFSET];
			// Do not replace with foreach. ConcurrentModificationException will arise.
			for (int k = 0; k < glyphsInList.Count; ++k)
			{
				int glyph = glyphsInList[k];
				CheckGlyphComposite(glyph);
			}
		}

		/// <exception cref="System.IO.IOException"/>
		protected internal virtual void CheckGlyphComposite(int glyph)
		{
			int start = locaTable[glyph];
			if (start == locaTable[glyph + 1])
			{
				// no contour
				return;
			}
			rf.Seek(tableGlyphOffset + start);
			int numContours = rf.ReadShort();
			if (numContours >= 0)
			{
				return;
			}
			rf.SkipBytes(8);
			for (; ; )
			{
				int flags = rf.ReadUnsignedShort();
				int cGlyph = rf.ReadUnsignedShort();
				if (!glyphsUsed.Contains(cGlyph))
				{
					glyphsUsed.Add(cGlyph);
					glyphsInList.Add(cGlyph);
				}
				if ((flags & MORE_COMPONENTS) == 0)
				{
					return;
				}
				int skip;
				if ((flags & ARG_1_AND_2_ARE_WORDS) != 0)
				{
					skip = 4;
				}
				else
				{
					skip = 2;
				}
				if ((flags & WE_HAVE_A_SCALE) != 0)
				{
					skip += 2;
				}
				else
				{
					if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0)
					{
						skip += 4;
					}
				}
				if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0)
				{
					skip += 8;
				}
				rf.SkipBytes(skip);
			}
		}

		/// <summary>
		/// Reads a
		/// <c>String</c>
		/// from the font file as bytes using the Cp1252 encoding.
		/// </summary>
		/// <param name="length">the length of bytes to read</param>
		/// <returns>
		/// the
		/// <c>String</c>
		/// read
		/// </returns>
		/// <exception cref="System.IO.IOException">the font file could not be read</exception>
		protected internal virtual String ReadStandardString(int length)
		{
			byte[] buf = new byte[length];
			rf.ReadFully(buf);
			try
			{
				return iTextSharp.IO.Util.JavaUtil.GetStringForBytes(buf, PdfEncodings.WINANSI);
			}
			catch (Exception e)
			{
				throw new IOException("TrueType font", e);
			}
		}

		protected internal virtual void WriteFontShort(int n)
		{
			outFont[fontPtr++] = (byte)(n >> 8);
			outFont[fontPtr++] = (byte)n;
		}

		protected internal virtual void WriteFontInt(int n)
		{
			outFont[fontPtr++] = (byte)(n >> 24);
			outFont[fontPtr++] = (byte)(n >> 16);
			outFont[fontPtr++] = (byte)(n >> 8);
			outFont[fontPtr++] = (byte)n;
		}

		protected internal virtual void WriteFontString(String s)
		{
			byte[] b = PdfEncodings.ConvertToBytes(s, PdfEncodings.WINANSI);
			System.Array.Copy(b, 0, outFont, fontPtr, b.Length);
			fontPtr += b.Length;
		}

		protected internal virtual int CalculateChecksum(byte[] b)
		{
			int len = b.Length / 4;
			int v0 = 0;
			int v1 = 0;
			int v2 = 0;
			int v3 = 0;
			int ptr = 0;
			for (int k = 0; k < len; ++k)
			{
				v3 += b[ptr++] & 0xff;
				v2 += b[ptr++] & 0xff;
				v1 += b[ptr++] & 0xff;
				v0 += b[ptr++] & 0xff;
			}
			return v0 + (v1 << 8) + (v2 << 16) + (v3 << 24);
		}
	}
}