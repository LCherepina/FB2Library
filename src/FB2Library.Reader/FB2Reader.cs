﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using FB2Library.Elements;
using FB2Library.Elements.Poem;
using FB2Library.Reader.Interfaces;
using FB2Library.Reader.Lines;

namespace FB2Library.Reader
{
	public class FB2Reader : IDisposable
	{
		private XmlReaderSettings _settings;
		private IList<ILine> _lines;
		private FB2File _file;

		public FB2Reader(XmlReaderSettings settings = null)
		{
			if (settings == null)
			{
				_settings = new XmlReaderSettings
				{
					DtdProcessing = DtdProcessing.Ignore
				};
			}
			else
			{
				_settings = settings;
			}

			_lines = new List<ILine>();
		}

		public void Dispose()
		{
			_settings = null;
			_lines.Clear();
			_file = null;
		}

		public Task<FB2File> LoadAsync(Stream stream, LoadOptions options = LoadOptions.PreserveWhitespace)
		{
			return Task.Factory.StartNew(() =>
			{
				var file = new FB2File();
				using (var reader = XmlReader.Create(stream, _settings))
				{
					var fb2Document = XDocument.Load(reader, options);
					file.Load(fb2Document, false);
				}
				return file;
			});
		}

		public Task<FB2File> LoadAsync(string xml)
		{
			return Task.Factory.StartNew(() =>
			{
				var file = new FB2File();
				var fb2Document = XDocument.Parse(xml);
				file.Load(fb2Document, false);
				return file;
			});
		}

		public virtual async Task<IEnumerable<ILine>> ReadAsync(FB2File file)
		{
			return await Task.Factory.StartNew(() =>
			{
				_file = file;

				if (_file.MainBody != null)
				{
					PrepareBodies();
				}

				return _lines;
			});
		}

		protected virtual void PrepareBodies()
		{
			foreach (var bodyItem in _file.Bodies)
			{
				AddTitle(bodyItem.Title);

				foreach (SectionItem sectionItem in bodyItem.Sections)
				{
					PrepareTextItem(sectionItem);
				}
			}
		}

		protected virtual void PrepareTextItems(IEnumerable<IFb2TextItem> textItems)
		{
			foreach (var textItem in textItems)
			{
				if (textItem is IFb2TextItem)
				{
					PrepareTextItem(textItem);
				}
				else
				{
					AddTextLine(textItem.ToString());
				}
			}
		}

		protected virtual void PrepareTextItem(IFb2TextItem textItem)
		{
			if (textItem is CiteItem)
			{
				PrepareTextItems(((CiteItem)textItem).CiteData);
				return;
			}

			if (textItem is PoemItem)
			{
				var item = (PoemItem)textItem;
				AddTitle(item.Title);
				PrepareTextItems(item.Content);
				return;
			}

			if (textItem is SectionItem)
			{
				var item = (SectionItem)textItem;
				AddTitle(item.Title);
				PrepareTextItems(item.Content);
				return;
			}

			if (textItem is StanzaItem)
			{
				var item = (StanzaItem)textItem;
				AddTitle(item.Title);
				PrepareTextItems(item.Lines);
				return;
			}

			if (textItem is ParagraphItem
				|| textItem is EmptyLineItem
				|| textItem is TitleItem
				|| textItem is SimpleText)
			{
				AddTextLine(textItem.ToString());
				return;
			}

			if (textItem is ImageItem)
			{
				var item = (ImageItem)textItem;
				var key = item.HRef.Replace("#", string.Empty);

				if (_file.Images.ContainsKey(key))
				{
					var data = _file.Images[key].BinaryData;
					_lines.Add(new ImageLine { Data = data });
				}
				return;
			}

			if (textItem is DateItem)
			{
				AddTextLine(((DateItem)textItem).DateValue.ToString());
				return;
			}

			if (textItem is EpigraphItem)
			{
				var item = (EpigraphItem) textItem;
				PrepareTextItems(item.EpigraphData);
				return;
			}

			throw new Exception(textItem.GetType().ToString());
		}

		protected virtual void AddTitle(TitleItem titleItem)
		{
			if (titleItem != null)
			{
				foreach (var title in titleItem.TitleData)
				{
					_lines.Add(new HeaderLine { Text = title.ToString() });
				}
			}
		}

		protected virtual void AddTextLine(string text)
		{
			_lines.Add(new TextLine { Text = text });
		}
	}
}