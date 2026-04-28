//! # X.509 Validator Modul
//! 
//! Dieses Modul bietet eine schlanke, `no_std`-kompatible Validierung für ASN.1-Header,
//! wie sie in X.509-Zertifikaten verwendet werden. Es ist primär für die FFI-Kommunikation
//! (z.B. mit C#/.NET) optimiert.

#![no_std]

#[cfg(not(test))]
use core::panic::PanicInfo;

/// Validiert, ob ein Puffer einen gültigen X.509-ASN.1-Header besitzt.
/// 
/// # Safety
/// Diese Funktion ist Teil der FFI-Schnittstelle. Der Aufrufer muss sicherstellen,
/// dass `ptr` ein valider Pointer auf `len` Bytes ist.
#[unsafe(no_mangle)]
pub extern "C" fn validate_x509_structure(ptr: *const u8, len: usize) -> bool {
    // Einfache Prüfung ob gültige Pointer/Längen an die Funktion übergeben wurde.
    if ptr.is_null() || len == 0 {
        return false;
    }

    // Erzeuge ein Slice aus dem Pointer. 
    let data = unsafe { core::slice::from_raw_parts(ptr, len) };

    match Asn1Header::parse(data) {
        Ok(header) => {
            if header.tag == Asn1Tag::Sequence {
                return (header.header_bytes + header.length) <= data.len();
            }
            false
        },
        Err(_) => false,
    }
}

pub struct Asn1Header {
    pub tag: Asn1Tag,
    pub length: usize,
    pub header_bytes: usize,
}

impl Asn1Header {
    pub fn parse(data: &[u8]) -> Result<Self, &'static str> {
        if data.is_empty() { return  Err("Buffer leer"); }

        let tag = Asn1Tag::try_from(data[0]).map_err(|_| "Unbekannter Tag")?;

        if data.len() < 2 { return Err("Header unvollständig"); }

        let first_len_byte = data[1];

        // Bit 8 ist 0 -> Short Form (Länge passt in ein Byte: 0-127)
        // 0x80 -> 1000_0000b
        if first_len_byte & 0x80 == 0  {
            Ok(Asn1Header {
                tag,
                length: first_len_byte as usize,
                header_bytes: 2
            })
        }
        // Bit 8 ist 1 -> Long Form (Länge erstreckt sich über mehrere Bytes)
        else {
            // 0x7F -> 0111_1111b
            let num_len_bytes =(first_len_byte & 0x7F) as usize;

            // X.509 Zertifikate sollten keine absurden Längenfelder haben (4 Bytes = 4GB sollten reichen)
            if num_len_bytes == 0 || num_len_bytes > 4 {
                return Err("Ungültige Längen-Kodierung"); 
            }
            if data.len() < 2 + num_len_bytes { 
                return Err("Puffer zu kurz für Long-Form"); 
            }

            // Big-Endian Rekonstruktion:
            // Wir bauen die Zahl Byte für Byte zusammen.
            let mut length: usize = 0;
            for i in 0..num_len_bytes {
                length = (length << 8) | (data[2 + i] as usize);
            }

            Ok(Asn1Header { 
                tag, 
                length, 
                header_bytes: 2 + num_len_bytes 
            })
        }
    }
}

/// ASN.1 Tags basierend auf DER-Kodierung (X.690 Spezifikation).
#[repr(u8)]
#[derive(Debug, PartialEq, Eq, Copy, Clone)]
pub enum Asn1Tag {
    Sequence = 0x30,
    Set = 0x31,
    PrintableString = 0x13,
    Utf8String = 0x0C,
    Integer = 0x02,
    BitString = 0x03,
    OctetString = 0x04,
    Null = 0x05,
    ObjectId = 0x06,
    UtcTime = 0x17,
    GeneralizedTime = 0x18,
}

impl TryFrom<u8> for Asn1Tag {
    type Error = ();

    fn try_from(value: u8) -> Result<Self, Self::Error> {
        match value {
            0x30 => Ok(Asn1Tag::Sequence),
            0x31 => Ok(Asn1Tag::Set),
            0x02 => Ok(Asn1Tag::Integer),
            0x03 => Ok(Asn1Tag::BitString),
            0x04 => Ok(Asn1Tag::OctetString),
            0x05 => Ok(Asn1Tag::Null),
            0x06 => Ok(Asn1Tag::ObjectId),
            0x17 => Ok(Asn1Tag::UtcTime),
            0x18 => Ok(Asn1Tag::GeneralizedTime),
            0x13 => Ok(Asn1Tag::PrintableString),
            0x0C => Ok(Asn1Tag::Utf8String),
            _ => Err(()),
        }
    }
}

/// Panic-Handler für Umgebungen ohne Standard-Bibliothek.
/// Da wir hier nicht "unwinden" können, wird die Ausführung bei einem Fehler gestoppt.
#[cfg(not(test))]
#[panic_handler]
fn panic(_info: &PanicInfo) -> ! {
    loop {}
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_valid_asn1_sequence() {
        // Beispiel-Daten einer validen Sequenz
        let data: [u8; 19] = [0x30, 0x11, 0x31, 0x0f, 0x30, 0x0d, 0x06, 0x03, 
                              0x55, 0x04, 0x03, 0x13, 0x06, 0x54, 0x65, 0x73,
                              0x74, 0x43, 0x4e];
        let result = validate_x509_structure(data.as_ptr(), data.len());
        assert!(result, "Gültige Sequenz sollte als valid erkannt werden.");
    }

    #[test]
    fn test_invalid_header_byte() {
        let data: [u8; 3] = [0xFF, 0x00, 0x01];
        let result = validate_x509_structure(data.as_ptr(), data.len());
        assert!(!result, "Header ohne 0x30 sollte abgelehnt werden.");
    }

    #[test]
    fn test_buffer_overflow_protection() {
        let data: [u8; 2] = [0x30, 0x0A]; // Header Lügt über länge, er sagt er hat 10 Bytes obwohl in echt nur 2 Bytes
        let result = validate_x509_structure(data.as_ptr(), data.len());
        assert!(!result, "Längenangabe, die über Puffer hinausgeht, muss ablehnen.");
    }

    #[test]
    fn test_null_pointer() {
        let result = validate_x509_structure(core::ptr::null(), 0);
        assert!(!result, "Null-Pointer sollte sicher abgelehnt werden.");
    }
}
