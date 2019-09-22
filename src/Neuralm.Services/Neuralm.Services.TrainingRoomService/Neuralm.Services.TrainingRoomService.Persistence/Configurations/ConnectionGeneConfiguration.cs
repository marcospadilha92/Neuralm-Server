﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Neuralm.Services.TrainingRoomService.Domain;

namespace Neuralm.Services.TrainingRoomService.Persistence.Configurations
{
    /// <summary>
    /// Represents the <see cref="ConnectionGeneConfiguration"/> class used to configure the relations and columns in the <see cref="DbSet{TEntity}"/> for <see cref="ConnectionGene"/> in the DbContext.
    /// </summary>
    public class ConnectionGeneConfiguration : IEntityTypeConfiguration<ConnectionGene>
    {
        /// <inheritdoc cref="IEntityTypeConfiguration{TEntity}.Configure"/>
        public void Configure(EntityTypeBuilder<ConnectionGene> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Ignore(p => p.InNode);
            builder.Ignore(p => p.OutNode);
        }
    }
}
