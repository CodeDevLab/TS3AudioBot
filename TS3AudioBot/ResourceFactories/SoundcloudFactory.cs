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
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using System.Web.Script.Serialization;
	using Helper;

	public sealed class SoundcloudFactory : IResourceFactory
	{
		private JavaScriptSerializer jsonParser;

		public AudioType FactoryFor => AudioType.Soundcloud;
		public string SoundcloudClientID { get; private set; }

		public SoundcloudFactory()
		{
			jsonParser = new JavaScriptSerializer();
			SoundcloudClientID = "a9dd3403f858e105d7e266edc162a0c5";
		}

		public bool MatchLink(string link) => Regex.IsMatch(link, @"^https?\:\/\/(www\.)?soundcloud\.");

		public RResultCode GetResource(string link, out AudioResource resource)
		{
			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/resolve.json?url={Uri.EscapeUriString(link)}&client_id={SoundcloudClientID}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
			{
				resource = null;
				return RResultCode.ScInvalidLink;
			}
			var parsedDict = ParseJson(jsonResponse);
			int id = (int)parsedDict["id"];
			string title = (string)parsedDict["title"];
			return GetResourceById(id.ToString(), title, out resource);
		}

		public RResultCode GetResourceById(string id, string name, out AudioResource resource)
		{
			string finalRequest = $"https://api.soundcloud.com/tracks/{id}/stream?client_id={SoundcloudClientID}";
			resource = new SoundcloudResource(id, name, finalRequest);
			return RResultCode.Success;
		}

		public string RestoreLink(string id)
		{
			string jsonResponse;
			var uri = new Uri($"https://api.soundcloud.com/tracks/{id}?client_id={SoundcloudClientID}");
			if (!WebWrapper.DownloadString(out jsonResponse, uri))
				return null;
			var parsedDict = ParseJson(jsonResponse);
			return (string)parsedDict["permalink_url"];
		}

		private Dictionary<string, object> ParseJson(string jsonResponse) => (Dictionary<string, object>)jsonParser.DeserializeObject(jsonResponse);

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			abortPlay = false;
		}

		public void Dispose() { }
	}

	public sealed class SoundcloudResource : AudioResource
	{
		public override AudioType AudioType => AudioType.Soundcloud;

		public string ResourceURL { get; private set; }

		public SoundcloudResource(string id, string name, string url)
			: base(id, name)
		{
			ResourceURL = url;
		}

		public override string Play()
		{
			return ResourceURL;
		}
	}
}
