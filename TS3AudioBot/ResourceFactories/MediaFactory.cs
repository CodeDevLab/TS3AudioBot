// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using System.IO;
	using Helper;
	using Helper.AudioTags;
	using CommandSystem;

	public sealed class MediaFactory : IResourceFactory
	{
		public AudioType FactoryFor => AudioType.MediaLink;

		public bool MatchLink(string uri) => true;

		public RResultCode GetResource(string uri, out AudioResource resource)
		{
			return GetResourceById(uri, null, out resource);
		}

		public RResultCode GetResourceById(string id, string name, out AudioResource resource)
		{
			var result = ValidateUri(id, ref name, id);

			if (result == RResultCode.MediaNoWebResponse)
			{
				resource = null;
				return result;
			}
			else
			{
				resource = new MediaResource(id, name, id, result);
				return RResultCode.Success;
			}
		}

		public string RestoreLink(string id) => id;

		private static RResultCode ValidateUri(string id, ref string name, string uri)
		{
			Uri uriResult;
			if (!Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out uriResult))
				return RResultCode.MediaInvalidUri;

			try
			{
				string scheme = uriResult.Scheme;
				if (scheme == Uri.UriSchemeHttp
					|| scheme == Uri.UriSchemeHttps
					|| scheme == Uri.UriSchemeFtp)
					return ValidateWeb(id, ref name, uri);
				else if (uriResult.Scheme == Uri.UriSchemeFile)
					return ValidateFile(id, ref name, uri);
				else
					return RResultCode.MediaUnknownUri;
			}
			catch (InvalidOperationException)
			{
				return ValidateFile(id, ref name, uri);
			}
		}

		private static void GetStreamData(string id, ref string name, Stream stream)
		{
			if (string.IsNullOrEmpty(name))
			{
				if (stream != null)
				{
					name = AudioTagReader.GetTitle(stream);
					name = string.IsNullOrWhiteSpace(name) ? id : name;
				}
				else name = id;
			}
		}

		private static RResultCode ValidateWeb(string id, ref string name, string link)
		{
			string refname = name;
			if (WebWrapper.GetResponse(new Uri(link), response => { using (var stream = response.GetResponseStream()) GetStreamData(id, ref refname, stream); }) != ValidateCode.Ok)
				return RResultCode.MediaNoWebResponse;

			name = refname;
			return RResultCode.Success;
		}

		private static RResultCode ValidateFile(string id, ref string name, string path)
		{
			if (!File.Exists(path))
				return RResultCode.MediaFileNotFound;

			try
			{
				using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
				{
					GetStreamData(id, ref name, stream);
					return RResultCode.Success;
				}
			}
			catch (PathTooLongException) { return RResultCode.AccessDenied; }
			catch (DirectoryNotFoundException) { return RResultCode.MediaFileNotFound; }
			catch (FileNotFoundException) { return RResultCode.MediaFileNotFound; }
			catch (IOException) { return RResultCode.AccessDenied; }
			catch (UnauthorizedAccessException) { return RResultCode.AccessDenied; }
			catch (NotSupportedException) { return RResultCode.AccessDenied; }
		}

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			MediaResource mediaResource = (MediaResource)data.Resource;
			if (mediaResource.InternalResultCode == RResultCode.Success)
			{
				abortPlay = false;
			}
			else
			{
				abortPlay = true;
				data.Session.Write(
					$"This uri might be invalid ({mediaResource.InternalResultCode}), do you want to start anyway?");
				data.Session.UserResource = data;
				data.Session.SetResponse(ResponseValidation, null);
			}
		}

		private static bool ResponseValidation(ExecutionInformation info)
		{
			Answer answer = TextUtil.GetAnswer(info.TextMessage.Message);
			if (answer == Answer.Yes)
			{
				PlayData data = info.Session.UserResource;
				info.Session.Bot.FactoryManager.Play(data);
			}
			else if (answer == Answer.No)
			{
				info.Session.UserResource = null;
			}
			return answer != Answer.Unknown;
		}

		public void Dispose()
		{

		}
	}

	public sealed class MediaResource : AudioResource
	{
		public override AudioType AudioType => AudioType.MediaLink;

		public string ResourceURL { get; private set; }
		public RResultCode InternalResultCode { get; private set; }

		public MediaResource(string id, string name, string url, RResultCode internalRC)
			: base(id, name)
		{
			ResourceURL = url;
			InternalResultCode = internalRC;
		}

		public override string Play()
		{
			return ResourceURL;
		}
	}
}
