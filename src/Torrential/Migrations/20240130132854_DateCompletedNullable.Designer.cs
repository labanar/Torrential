﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Torrential;

#nullable disable

namespace Torrential.Migrations
{
    [DbContext(typeof(TorrentialDb))]
    [Migration("20240130132854_DateCompletedNullable")]
    partial class DateCompletedNullable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.1");

            modelBuilder.Entity("Torrential.TorrentConfiguration", b =>
                {
                    b.Property<string>("InfoHash")
                        .HasColumnType("TEXT");

                    b.Property<string>("CompletedPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset?>("DateCompleted")
                        .HasColumnType("TEXT");

                    b.Property<string>("DownloadPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("InfoHash");

                    b.ToTable("Torrents");
                });
#pragma warning restore 612, 618
        }
    }
}
