export interface GstParty { name: string; address: string; gstin: string | null; }
export interface GstWorksheetRequest {
  version: string;
  supplier: GstParty;
  recipient: GstParty;
  placeOfSupplyCode: string;
  placeOfSupplyName: string;
  reverseCharge: boolean;
  items: { variantId: string; hsn: string; unitCode: string }[];
  shippingSac: string | null;
}
export interface GstWorksheet {
  documentType: 'GST_WORKSHEET_DRAFT';
  orderNumber: string;
  orderDate: string;
  externalInvoiceReference: string | null;
  currency: string;
  supplier: GstParty;
  recipient: GstParty;
  deliveryAddress: string;
  placeOfSupplyCode: string;
  placeOfSupplyName: string;
  reverseCharge: boolean;
  lines: { sku: string; description: string; classification: string; unitCode: string; quantity: number; discount: number;
    taxableValue: number; recordedTaxRate: number; recordedTax: number; total: number }[];
  shipping: { method: string; sac: string | null; taxableValue: number; recordedTax: number; total: number };
  recordedTax: number;
  total: number;
}
