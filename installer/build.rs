fn main() {
    // Windows target: ask for admin (UAC) because browser policies live in HKLM.
    if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows") {
        embed_manifest::embed_manifest(
            embed_manifest::new_manifest("InvoiceScraper.Setup")
                .requested_execution_level(embed_manifest::manifest::ExecutionLevel::RequireAdministrator),
        )
        .expect("embed manifest");
    }
    println!("cargo:rerun-if-changed=build.rs");
}
