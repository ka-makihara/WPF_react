// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd.Models
{
	public class Genre : ViewModelBase
	{
		private int _genreId;
		private string? _name;
		private string? _description;
		private List<Album>? _albums;

		public int GenreId
		{
			get => _genreId;
			set => this.SetProperty(ref _genreId, value);
		}

		public string? Name
		{
			get => _name;
			set => this.SetProperty(ref _name, value);
		}

		public string? Description
		{
			get => _description;
			set => this.SetProperty(ref _description, value);
		}

		public List<Album>? Albums
		{
			get => _albums;
			set => this.SetProperty(ref _albums, value);
		}

#if NET5_0_OR_GREATER || NETCOREAPP
		public override string? ToString()
#else
        public override string ToString()
#endif
		{
			return Name ?? base.ToString();
		}
	}
}