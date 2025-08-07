// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Exception thrown when Belay.NET configuration is invalid.
/// </summary>
public class BelayConfigurationException : BelayException {
    /// <summary>
    /// Gets the configuration section that caused the error.
    /// </summary>
    public string? ConfigurationSection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="configurationSection">The configuration section that caused the error.</param>
    public BelayConfigurationException(string message, string? configurationSection = null)
        : base(message, "BELAY_CONFIG_ERROR", nameof(BelayConfigurationException)) {
        this.ConfigurationSection = configurationSection;

        if (configurationSection != null) {
            this.WithContext("configuration_section", configurationSection);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="configurationSection">The configuration section that caused the error.</param>
    public BelayConfigurationException(string message, Exception innerException, string? configurationSection = null)
        : base(message, innerException, "BELAY_CONFIG_ERROR", nameof(BelayConfigurationException)) {
        this.ConfigurationSection = configurationSection;

        if (configurationSection != null) {
            this.WithContext("configuration_section", configurationSection);
        }
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_CONFIG_ERROR";
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class BelayValidationException : BelayException {
    /// <summary>
    /// Gets the validation target that failed.
    /// </summary>
    public string ValidationTarget { get; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<string> ValidationErrors { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="validationTarget">The validation target that failed.</param>
    /// <param name="errors">The validation errors.</param>
    public BelayValidationException(string message, string validationTarget, IEnumerable<string>? errors = null)
        : base(message, "BELAY_VALIDATION_ERROR", nameof(BelayValidationException)) {
        this.ValidationTarget = validationTarget;
        if (errors != null) {
            this.ValidationErrors.AddRange(errors);
        }

        this.WithContext("validation_target", validationTarget)
            .WithContext("validation_errors", this.ValidationErrors);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="validationTarget">The validation target that failed.</param>
    /// <param name="errors">The validation errors.</param>
    public BelayValidationException(string message, Exception innerException, string validationTarget, IEnumerable<string>? errors = null)
        : base(message, innerException, "BELAY_VALIDATION_ERROR", nameof(BelayValidationException)) {
        this.ValidationTarget = validationTarget;
        if (errors != null) {
            this.ValidationErrors.AddRange(errors);
        }

        this.WithContext("validation_target", validationTarget)
            .WithContext("validation_errors", this.ValidationErrors);
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_VALIDATION_ERROR";
}
