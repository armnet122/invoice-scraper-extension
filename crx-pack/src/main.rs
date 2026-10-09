//! Dev tool: zips ./extension, signs it as a CRX3 with keys/extension.pem,
//! writes releases/invoice-scraper-<ver>.crx and updates.xml.
use rsa::pkcs8::DecodePrivateKey;
use rsa::pkcs8::EncodePublicKey;
use rsa::{
    pkcs1v15::SigningKey,
    signature::{SignatureEncoding, Signer},
    RsaPrivateKey,
};
use sha2::{Digest, Sha256};
use std::io::{Cursor, Write};

const REPO_RAW: &str = "https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main";

fn varint(mut n: usize, out: &mut Vec<u8>) {
    while n >= 0x80 {
        out.push((n as u8) | 0x80);
        n >>= 7;
    }
    out.push(n as u8);
}

/// Protobuf length-delimited field.
fn field(num: u32, data: &[u8], out: &mut Vec<u8>) {
    varint(((num << 3) | 2) as usize, out);
    varint(data.len(), out);
    out.extend_from_slice(data);
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let manifest: serde_json::Value = serde_json::from_slice(&std::fs::read("extension/manifest.json")?)?;
    let version = manifest["version"].as_str().ok_or("manifest has no version")?;

    let mut zip_buf = Cursor::new(Vec::new());
    {
        let mut zw = zip::ZipWriter::new(&mut zip_buf);
        let opts = zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Deflated);
        for entry in std::fs::read_dir("extension")? {
            let p = entry?.path();
            if p.is_file() {
                zw.start_file(p.file_name().unwrap().to_string_lossy(), opts)?;
                zw.write_all(&std::fs::read(&p)?)?;
            }
        }
        zw.finish()?;
    }
    let zip = zip_buf.into_inner();

    let key = RsaPrivateKey::from_pkcs8_pem(&std::fs::read_to_string("keys/extension.pem")?)?;
    let pub_der = key.to_public_key().to_public_key_der()?;
    let crx_id = &Sha256::digest(pub_der.as_bytes())[..16];

    let mut signed_data = Vec::new();
    field(1, crx_id, &mut signed_data);

    let mut to_sign = b"CRX3 SignedData\0".to_vec();
    to_sign.extend_from_slice(&(signed_data.len() as u32).to_le_bytes());
    to_sign.extend_from_slice(&signed_data);
    to_sign.extend_from_slice(&zip);
    let sig = SigningKey::<Sha256>::new(key).sign(&to_sign).to_vec();

    let mut proof = Vec::new();
    field(1, pub_der.as_bytes(), &mut proof);
    field(2, &sig, &mut proof);
    let mut header = Vec::new();
    field(2, &proof, &mut header);
    field(10000, &signed_data, &mut header);

    let mut crx = b"Cr24".to_vec();
    crx.extend_from_slice(&3u32.to_le_bytes());
    crx.extend_from_slice(&(header.len() as u32).to_le_bytes());
    crx.extend_from_slice(&header);
    crx.extend_from_slice(&zip);

    let name = format!("invoice-scraper-{version}.crx");
    std::fs::create_dir_all("releases")?;
    std::fs::write(format!("releases/{name}"), &crx)?;

    let id: String = crx_id.iter().flat_map(|b| [b >> 4, b & 15]).map(|n| (b'a' + n) as char).collect();
    std::fs::write(
        "updates.xml",
        format!(
            "<?xml version='1.0' encoding='UTF-8'?>\n<gupdate xmlns='http://www.google.com/update2/response' protocol='2.0'>\n  <app appid='{id}'>\n    <updatecheck codebase='{REPO_RAW}/releases/{name}' version='{version}' />\n  </app>\n</gupdate>\n"
        ),
    )?;
    println!("packed releases/{name} ({} bytes), extension id {id}", crx.len());
    Ok(())
}
